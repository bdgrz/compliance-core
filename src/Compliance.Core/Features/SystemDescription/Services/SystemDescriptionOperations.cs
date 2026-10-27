using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.SystemDescription.Services;

/// <summary>Exact native acceptance; source guards and immutable publication stay inside recovery-aware preparation.</summary>
public sealed class SystemDescriptionOperations(NativeOperationExecutor operations, SystemDescriptionReader descriptions,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<TRequest>(IRequestContext<TRequest> context, Uuid tenant, Uuid program,
        Uuid description, Uuid command, string kind, long? expected,
        Func<SystemDescriptionReadSnapshot, OperationProvenance, CancellationToken, ValueTask<Result>> prepare,
        Func<SystemDescriptionRules, OperationProvenance, object?>? receipt, CancellationToken ct) where TRequest : IRequestBase
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !SystemDescriptionCommandActor.TryGet(context.Actor, tenant, out var member, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A description decision requires a personal HTTP member actor."));
        if (tenant == Uuid.Empty || program == Uuid.Empty || description == Uuid.Empty || command == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A description operation requires exact identities and its positive original revision when required."));
        if (expected is <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The description requires its current positive original logical revision."));
        var target = new SystemDescriptionAggregate(tenant, program, description);
        var operation = OwnerProvenance.For(target, command, kind,
            ActorReference.ForMember(member, display), description.ToString(), expected is { } revision ? unchecked((ulong)revision) : null,
            AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow()) with
        { FirstOwner = target.Stream };
        FrozenSystemDescriptionMutation? frozen = null;
        ImmutableContentRef? result = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<SystemDescriptionAggregate>(AcceptedOperationBinding.Of(operation),
                () => new SystemDescriptionAggregate(tenant, program, description, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (kind == nameof(CreateSystemDescriptionDraft) ? owner.Header is not null : owner.Header is null || owner.ProgramId != program)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(kind == nameof(CreateSystemDescriptionDraft) ? RequestErrorKind.Conflict : RequestErrorKind.NotFound,
                                "The system description is not available for this operation."));
                        if (expected is { } original && owner.Revision != original)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The description requires its original expected logical revision."));
                        var snapshot = kind == nameof(BindSystemDescriptionToExamination)
                            ? await descriptions.ReadAsync(owner, program, token).ConfigureAwait(false)
                            : await descriptions.ReadCurrentAsync(owner, program, token).ConfigureAwait(false);
                        var before = snapshot.View;
                        var ready = await prepare(snapshot, operation, token).ConfigureAwait(false);
                        if (!ready.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                        frozen = await SystemDescriptionContent.FreezeAsync(content, before, snapshot.Rules, kind,
                            snapshot.Rules.PendingChanges.Count == 0 ? receipt?.Invoke(snapshot.Rules, operation) : null, token, snapshot.CurrentAuthority).ConfigureAwait(false);
                        frozen = frozen with { SourceSnapshotContent = snapshot.PreparedSourceSnapshotContent ?? owner.SourceSnapshotContent };
                        var response = await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                        result = response.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(response.Response);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(SystemDescriptionReadFailure.Of(exception));
                    }
                }, (owner, response) => owner.Append(SystemDescriptionFactFactory.Create(frozen!, sequence + 1, operation, response, result!),
                    frozen!, sequence, position)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result.Failure(AcceptedAppendResult.ConflictError);
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != UnitResult.Schema ||
                descriptor.Body is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
                throw new ImmutableContentIntegrityException("The original description response has another result contract.");
            await content.ReadJsonAsync<UnitResult>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            return Result.Success;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(SystemDescriptionReadFailure.Of(exception));
        }
    }
    public static Result Outcome(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
}
