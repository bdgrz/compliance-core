using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Releases the engagement content pin of a materialized personal decision. The materialized child fact is found
///     on its own authoritative owner, never trusted from the command; only then does the engagement record the exact
///     materialized decision pointer. A late older decision is recorded as history and never regresses the pointer.
/// </summary>
sealed class SettleEngagementPersonalDecisionHandler(IAggregateReader reader, IAggregateExecutor executor,
    TimeProvider clock, EngagementPersonalDecisionVerifier verifier)
    : IRequestHandler<SettleEngagementPersonalDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<SettleEngagementPersonalDecision> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementPersonalDecisionEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The settlement names another operation or generation.");
        var loaded = await verifier.LoadAsync(new NativeFactReference(command.Target, Uuid.Empty, 0), command.Intent,
            command.OperationId, command.EngagementId, command.CommandId, ct, DecisionSource.Child)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        if (command.Target != owner.Stream)
            return SourceUseReceiverSupport.Permanent("The settlement names another engagement stream.");

        var provenance = EngagementPersonalDecisionEffects.Child(command.CommandId,
            nameof(SettleEngagementPersonalDecision), command.Source.EventId, intent, owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (decision.Canceled is not null)
            return SourceUseReceiverSupport.Superseded("The personal decision was canceled and never materialized.",
                NativeFactReference.Of(owner.Stream, decision.Canceled));
        if (decision.Authorized is null)
            return Result<ReceiverOutcome>.Failure(EngagementPersonalDecisionVerifier.Transient(
                "The authorization of the personal decision is not visible yet."));

        var child = await ChildFactAsync(owner.TenantId, intent, ct).ConfigureAwait(false);
        if (child is null)
            return Result<ReceiverOutcome>.Failure(EngagementPersonalDecisionVerifier.Transient(
                "The materialized personal decision is not visible yet."));
        if (child != command.Source)
            return SourceUseReceiverSupport.Permanent("The source is not the materialized fact of this decision.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(owner.TenantId,
                    command.EngagementId, command.CommandId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.DecisionOf(command.OperationId) is { Materialized: { } existing })
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(fresh.Stream, existing), false)));
                        var materialized = fresh.MaterializePersonalDecision(provenance);
                        return materialized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(materialized.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery of the same settlement won the append; rehydrate and decide again.
            }
        }
    }

    /// <summary>The materialized child fact of the decision on its own owner, once visible.</summary>
    async ValueTask<NativeFactReference?> ChildFactAsync(Uuid tenantId, EngagementPersonalDecisionIntent intent,
        CancellationToken ct)
    {
        if (intent.Kind == EngagementPersonalDecisionKind.ManagementAcknowledgement)
        {
            var acknowledgement = await reader.HydrateAsync(
                new EngagementManagementAcknowledgement(tenantId, intent.DecisionId), ct).ConfigureAwait(false);
            return acknowledgement.Acknowledged is { } fact
                ? NativeFactReference.Of(acknowledgement.Stream, fact)
                : null;
        }

        var partner = await reader.HydrateAsync(new PartnerIndependenceDecision(tenantId, intent.DecisionId), ct)
            .ConfigureAwait(false);
        return partner.Recorded is { } recorded ? NativeFactReference.Of(partner.Stream, recorded) : null;
    }

    sealed record Step(NativeFactReference Fact, bool Applied);
}
