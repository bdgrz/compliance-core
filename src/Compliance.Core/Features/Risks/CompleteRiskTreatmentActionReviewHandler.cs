using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Appends <see cref="RiskTreatmentActionCompletionReviewed" /> once every cited evidence request is held under this
///     review's grant. The held fulfilment proofs are re-derived from the authoritative evidence requests and must equal
///     the manifest the command carries; the exact waiver proof, when one was required, is verified again at the declared
///     decision instant. A review that was canceled is a terminal no-op, a waiver that no longer verifies is a permanent
///     mismatch that aborts the review, and a grant that is not visible yet is a transient wait. The decision is
///     appended before any use is settled.
/// </summary>
sealed class CompleteRiskTreatmentActionReviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ISeparationOfDutiesWaiverProofResolver waivers, RiskActionReviewVerifier verifier,
    RiskActionReviewFences fences, TimeProvider clock) : IRequestHandler<CompleteRiskTreatmentActionReview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteRiskTreatmentActionReview> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The completion names another operation.");
        var review = await verifier.VerifyAcceptedAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, ct).ConfigureAwait(false);
        if (review.Error is not null)
            return Result<ReceiverOutcome>.Failure(review.Error);
        var (action, intent, decision, tenant) = (review.Action!, review.Intent!, review.Decision!, review.TenantId);
        var accepted = action.AcceptedReview(command.OperationId)!;
        if (command.Target != action.Stream || command.ActionId != intent.ActionId ||
            command.Generation != accepted.Generation)
            return SourceUseReceiverSupport.Permanent("The completion differs from the frozen review intent.");

        var now = clock.GetUtcNow();
        var provenance = RiskActionReviewEffects.Child(command.CommandId, nameof(CompleteRiskTreatmentActionReview),
            command.Source.EventId, intent.Header, action.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(action, binding, command.OperationId) is { } early)
            return early;

        var held = await RiskActionReviewProofs.HeldAsync(reader, content, review, accepted.Generation,
            command.CommandId, ct).ConfigureAwait(false);
        if (!held.IsSuccess)
            return Result<ReceiverOutcome>.Failure(held.Error!);
        var proofs = await FindingContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            RiskContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
        if (proofs != command.EvidenceUseProofs)
            return SourceUseReceiverSupport.Permanent(
                "The held fulfilment proofs differ from the manifest the review was completed with.");

        if (decision.WaiverProof is { } waiverReference)
        {
            SeparationOfDutiesWaiverProof proof;
            try
            {
                proof = await FindingContent.ReadAsync<SeparationOfDutiesWaiverProof>(content, tenant, waiverReference,
                    ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen waiver proof cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
            }

            var verified = await waivers.VerifyAsync(proof, intent.Header.AcceptedAt, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return verified.Error!.IsTransient
                    ? Result<ReceiverOutcome>.Failure(verified.Error)
                    : await fences.AbortAsync(review, command.Source, command.Intent,
                        "The separation-of-duties waiver proof no longer verifies.", null, context, ct).ConfigureAwait(false);
        }

        var (_, result) = await FindingContent.FreezeAsync(content, tenant, UnitResult.Schema, new UnitResult(), ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new RiskTreatmentAction(tenant, decision.ProgramId,
                    decision.RiskId, intent.ActionId, command.CommandId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CompleteReview(provenance, decision.Outcome, decision.Rationale,
                            decision.Reviewer, decision.DecidedAt, decision.SeparationOfDutiesWaiverId, command.Intent,
                            result);
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

    /// <summary>An exact replay, or a review that was canceled and so can never decide the completion.</summary>
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
            RiskTreatmentActionCompletionReviewCanceled canceled => Result<ReceiverOutcome>.Success(
                new ReceiverOutcome.Rejected("The review was canceled; settle its evidence uses.",
                    NativeFactReference.Of(action.Stream, canceled))),
            RiskTreatmentActionCompletionReviewed => SourceUseReceiverSupport.Permanent(
                "The completion was decided by another operation."),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
