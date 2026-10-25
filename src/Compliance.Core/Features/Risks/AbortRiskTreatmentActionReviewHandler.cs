using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Cancels the exact pending review after an authoritative permanent proof mismatch and fences its delayed
///     decision in the same append. A review that already decided the completion is an irreversible decision: the abort
///     is a terminal no-op and the review recovers forward. A review that is not visible yet is a transient wait.
/// </summary>
sealed class AbortRiskTreatmentActionReviewHandler(RiskActionReviewVerifier verifier, IAggregateExecutor executor,
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
        if (command.FailureProof.Schema != RiskContent.FailureSchema)
            return SourceUseReceiverSupport.Permanent("The cancellation requires the current exact admitted-review failure schema.");
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
        if (proof.Admission is null || proof.Intent is null || stream != command.Target || proof.OperationId != command.OperationId || proof.Generation != command.Generation ||
            proof.Admission.Stream != stream || proof.EvidenceRequestId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(proof.Reason))
            return SourceUseReceiverSupport.Permanent("The failure proof names another exact action, review or generation.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var review = await verifier.VerifyAcceptedAsync(proof.Admission, proof.Intent, command.OperationId,
                    command.CommandId, ct).ConfigureAwait(false);
                if (review.Error is not null)
                    return Result<ReceiverOutcome>.Failure(review.Error);
                var action = review.Action!;
                var intent = review.Intent!;
                var accepted = action.AcceptedReview(command.OperationId)!;
                if (accepted.Generation != command.Generation || accepted.Intent != proof.Intent ||
                    NativeFactReference.Of(action.Stream, accepted) != proof.Admission)
                    return SourceUseReceiverSupport.Permanent("The abort differs from the exact admitted review source.");
                if (proof.EvidenceRequestId is { } evidenceRequestId)
                {
                    try
                    {
                        var cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content,
                            tenant.ToString(), intent.CompletionEvidence, ct).ConfigureAwait(false);
                        if (cited.All(item => item.EvidenceRequestId != evidenceRequestId))
                            return SourceUseReceiverSupport.Permanent("The failure evidence is outside the frozen review selection.");
                    }
                    catch (ImmutableContentIntegrityException failure)
                    {
                        return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact cancellation evidence selection cannot be verified.", failure is ImmutableContentUnavailableException));
                    }
                }
                var provenance = RiskActionReviewEffects.Child(command.CommandId,
                    nameof(AbortRiskTreatmentActionReview), accepted.Metadata.EventId, intent.Header, action.Stream,
                    clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(action, binding, command.OperationId, command.FailureProof) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new RiskTreatmentAction(tenant, proof.ProgramId, proof.RiskId,
                    proof.ActionId, command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId, command.FailureProof) is { } decided)
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
    static Result<ReceiverOutcome>? Decide(RiskTreatmentAction action, AcceptedOperationBinding binding, Uuid operationId,
        ImmutableContentRef failureProof)
    {
        switch (action.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                if (action.Acceptance.SelectedFact is not RiskTreatmentActionCompletionReviewCanceled selected ||
                    selected.SourceProofs != failureProof)
                    return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
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
