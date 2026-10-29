using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Services;

/// <summary>
///     Runs one root native operation whose declared result is Unit on its first owner, with the handler order of the
///     design contract: resolve the retry from the owner's accepted facts, then (only for a new operation) the
///     feasibility checks that read other owners, the owner guard and the frozen first response, then the append.
///     An exact retry replays the original result even after the owner changed; a changed intent conflicts.
/// </summary>
public sealed class AdvisorUnitOperation(NativeOperationExecutor operations, IImmutableContentStore content)
{
    /// <summary>Executes the operation and returns its unit result.</summary>
    /// <param name="context">The dispatch context.</param>
    /// <param name="tenantId">The realm of the frozen content.</param>
    /// <param name="provenance">The frozen provenance; the retry binding derives from it.</param>
    /// <param name="hydrate">Builds a fresh owner instance selected for the logical id.</param>
    /// <param name="acceptance">The owner's acceptance of the selected logical id.</param>
    /// <param name="feasibility">Checks of other owners for a new operation; null or success lets the guard decide.</param>
    /// <param name="guard">The owner guard, evaluated against the hydrated state when preparing and when appending.</param>
    /// <param name="apply">Raises the native fact with the frozen first response and operation result.</param>
    /// <param name="ct">The cancellation token.</param>
    public async ValueTask<Result> ExecuteAsync<TAggregate>(IExecutionContext context, Uuid tenantId,
        OperationProvenance provenance, Func<TAggregate> hydrate, Func<TAggregate, NativeAcceptance> acceptance,
        Func<TAggregate, CancellationToken, ValueTask<Result>>? feasibility, Func<TAggregate, Result> guard,
        Func<TAggregate, ImmutableContentRef, ImmutableContentRef, Result> apply, CancellationToken ct)
        where TAggregate : Aggregate
    {
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? result = null;
        AdvisorPreparedCut? preparedCut = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<TAggregate>(binding, hydrate, acceptance,
            async (owner, token) =>
            {
                preparedCut = AdvisorPreparedCut.Capture(owner);
                if (feasibility is not null)
                {
                    var feasible = await feasibility(owner, token).ConfigureAwait(false);
                    if (!feasible.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(feasible.Error!);
                }
                var guarded = guard(owner);
                if (!guarded.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guarded.Error!);
                var frozen = await AdvisorContent.FreezeUnitAsync(content, tenantId, token).ConfigureAwait(false);
                result = frozen.Result;
                return Result<FirstResponseDescriptor>.Success(frozen.Response);
            }, (owner, frozen) =>
            {
                var guarded = guard(owner);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is not null && !preparedCut.Matches(owner)
                    ? AdvisorPreparedCut.Changed()
                    : apply(owner, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return AdvisorContent.UnitOutcome(outcome);
    }
}
