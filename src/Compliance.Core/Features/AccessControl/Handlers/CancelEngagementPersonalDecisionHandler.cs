using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Cancels an admitted personal decision before its authorization. The cancellation fences authorization against
///     late delivery and releases the engagement content pin. An authorized decision is irreversible and recovers
///     forward: the cancellation is then a permanent conflict, never silently applied or dropped.
/// </summary>
sealed class CancelEngagementPersonalDecisionHandler(IAggregateExecutor executor, TimeProvider clock,
    EngagementPersonalDecisionVerifier verifier)
    : IRequestHandler<CancelEngagementPersonalDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CancelEngagementPersonalDecision> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementPersonalDecisionEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The cancellation names another operation or generation.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId,
            command.EngagementId, command.CommandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, intent) = (loaded.Owner!, loaded.Intent!);
        if (command.Target != owner.Stream)
            return SourceUseReceiverSupport.Permanent("The cancellation names another engagement stream.");

        var provenance = EngagementPersonalDecisionEffects.Child(command.CommandId,
            nameof(CancelEngagementPersonalDecision), command.Source.EventId, intent, owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(owner.TenantId,
                    command.EngagementId, command.CommandId, command.OperationId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.ReplayCut.Digest != owner.ReplayCut.Digest)
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The engagement native prefix changed during receiver preparation; retry from its exact current cut.", true)));

                        if (fresh.DecisionOf(command.OperationId) is { Canceled: { } existing })
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(fresh.Stream, existing), false)));
                        var canceled = fresh.CancelPersonalDecision(provenance);
                        return canceled.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(canceled.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery or authorization won the append; rehydrate and decide again.
            }
        }
    }

    sealed record Step(NativeFactReference Fact, bool Applied);
}
