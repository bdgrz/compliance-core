using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

/// <summary>
///     Runs a root native admission whose declared result is Unit on an owner that is its own first, affected and
///     revision scope. The retry is resolved from the owner's accepted facts before anything mutable is read, the
///     response is frozen once, and an error the caller names as a terminal no-op maps to success without a fact.
/// </summary>
public static class UnitOperations
{
    /// <summary>
    ///     Runs the operation. <paramref name="owner" /> hydrates a fresh owner selected for a logical id,
    ///     <paramref name="prepare" /> checks guards without raising and <paramref name="apply" /> raises the native fact
    ///     under the provenance and frozen response it is given.
    /// </summary>
    public static async ValueTask<Result> RunAsync<TRequest, TOwner>(NativeOperationExecutor operations,
        IImmutableContentStore content, TimeProvider clock, IRequestContext<TRequest> context, ActorReference? actor,
        Uuid logicalId, string commandKind, string episode, ulong? originalExpectedRevision,
        Func<Uuid, TOwner> owner, Func<TOwner, NativeAcceptance> acceptance, Func<TOwner, Result> prepare,
        Func<TOwner, OperationProvenance, ImmutableContentRef, Result> apply, params RequestError[] terminalNoOps)
        where TRequest : IRequest where TOwner : Aggregate
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        if (actor is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."));
        if (logicalId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A command id is required."));
        var provenance = OwnerProvenance.For(owner(Uuid.Empty), logicalId, commandKind, actor, episode,
            originalExpectedRevision, AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        var response = await UnitResponses.FreezeAsync(content, provenance.Owner.Realm).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TOwner>(
            AcceptedOperationBinding.Of(provenance), () => owner(logicalId), acceptance,
            current =>
            {
                var prepared = prepare(current);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (TOwner current, ImmutableContentRef frozen) => apply(current, provenance, frozen)), context)
            .ConfigureAwait(false);
        return UnitResponses.ToResult(accepted, terminalNoOps);
    }
}
