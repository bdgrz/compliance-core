using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Cancels the exact pending review after an authoritative permanent proof mismatch and fences its delayed
///     decision in the same append. A review that already decided the completion is an irreversible decision: the abort
///     is a terminal no-op and the review recovers forward. A review that is not visible yet is a transient wait.
/// </summary>
sealed class AbortRiskTreatmentActionReviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AbortRiskTreatmentActionReview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortRiskTreatmentActionReview> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The abort names another operation, generation or realm.");
        RiskActionReviewFailureProof proof;
        try
        {
            proof = await FindingContent.ReadAsync<RiskActionReviewFailureProof>(content, tenant, command.FailureProof,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The failure proof cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var stream = new RiskTreatmentAction(tenant, proof.ProgramId, proof.RiskId, proof.ActionId).Stream;
        if (stream != command.Target)
            return SourceUseReceiverSupport.Permanent("The failure proof names another action.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var action = await reader.HydrateAsync(new RiskTreatmentAction(tenant, proof.ProgramId, proof.RiskId,
                    proof.ActionId, command.CommandId), ct).ConfigureAwait(false);
                if (action.AcceptedReview(command.OperationId) is not { } accepted)
                    return SourceUseReceiverSupport.Transient("The accepted review is not visible yet.");
                RiskActionCompletionReviewIntent intent;
                try
                {
                    intent = await FindingContent.ReadAsync<RiskActionCompletionReviewIntent>(content, tenant,
                        accepted.Intent, ct).ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen review cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
                }

                if (accepted.Generation != command.Generation || intent.Header.RootOperationId != command.OperationId)
                    return SourceUseReceiverSupport.Permanent("The abort differs from the accepted review.");
                var provenance = RiskActionReviewEffects.Child(command.CommandId,
                    nameof(AbortRiskTreatmentActionReview), accepted.Metadata.EventId, intent.Header, action.Stream,
                    clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(action, binding, command.OperationId) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new RiskTreatmentAction(tenant, proof.ProgramId, proof.RiskId,
                    proof.ActionId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CancelReview(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.TerminalReview(command.OperationId)!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The action changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, a review that already decided the completion, or one canceled by another operation.</summary>
    static Result<ReceiverOutcome>? Decide(RiskTreatmentAction action, AcceptedOperationBinding binding, Uuid operationId)
    {
        switch (action.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(action.Stream,
                    action.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return action.TerminalReview(operationId) switch
        {
            RiskTreatmentActionCompletionReviewed reviewed => SourceUseReceiverSupport.Superseded(
                "The review already decided the completion; it recovers forward.",
                NativeFactReference.Of(action.Stream, reviewed)),
            RiskTreatmentActionCompletionReviewCanceled canceled => SourceUseReceiverSupport.Replayed(
                NativeFactReference.Of(action.Stream, canceled)),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
