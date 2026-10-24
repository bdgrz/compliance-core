using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Ends a pending review that can definitely never decide the completion: the review is canceled first, which
///     fences the delayed decision against late delivery, and only then is every evidence request use settled as
///     canceled, including the uses that were never acquired, so a delayed acquisition is refused. All effects use the
///     deterministic command ids the process driver would use, so a later driver step replays them. A review that
///     already decided recovers forward. Nothing is inferred from missing native state.
/// </summary>
sealed class RiskActionReviewFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(RiskActionReview review, NativeFactReference source,
        ImmutableContentRef intentReference, string reason, Uuid? evidenceRequestId, IExecutionContext context,
        CancellationToken ct)
    {
        var (action, intent, decision) = (review.Action!, review.Intent!, review.Decision!);
        var accepted = action.AcceptedReview(intent.Header.RootOperationId)
            ?? throw new InvalidOperationException("A fence needs the accepted review.");
        var episode = intent.Header.Episode;
        var proof = await FindingContent.WriteDeterministicAsync(content, review.TenantId, intent.Header.RootOperationId,
            RiskContent.FailureSchema, new RiskActionReviewFailureProof(decision.ProgramId, decision.RiskId,
                intent.ActionId, reason, evidenceRequestId), ct).ConfigureAwait(false);
        var abortId = RiskActionReviewEffects.ReceiverCommandId(source.EventId, nameof(AbortRiskTreatmentActionReview),
            null, episode);
        var aborted = await bus.SendAsync(new AbortRiskTreatmentActionReview(abortId, intent.Header.RootOperationId,
            action.Stream, accepted.Generation, 0, proof), context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A review that already decided the completion recovers forward: its own settlement releases the uses.
        if (aborted.Value is not (ReceiverOutcome.Applied or ReceiverOutcome.Replayed))
            return aborted;
        var canceled = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => throw new InvalidOperationException("An abort that was not applied is handled above."),
        };
        var settled = await SettleCanceledAsync(review, canceled, source, intentReference, context, ct)
            .ConfigureAwait(false);
        return settled.IsSuccess ? SourceUseReceiverSupport.Superseded(reason, canceled) : settled;
    }

    /// <summary>Settles every evidence request use of a canceled review as canceled, in canonical key order.</summary>
    public async ValueTask<Result<ReceiverOutcome>> SettleCanceledAsync(RiskActionReview review,
        NativeFactReference canceled, NativeFactReference source, ImmutableContentRef intentReference,
        IExecutionContext context, CancellationToken ct)
    {
        var (action, intent, decision) = (review.Action!, review.Intent!, review.Decision!);
        var tenant = review.TenantId;
        var generation = action.AcceptedReview(intent.Header.RootOperationId)?.Generation ?? 0;
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        try
        {
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, tenant.ToString(),
                intent.CompletionEvidence, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The cited evidence manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        foreach (var item in cited.OrderBy(item => RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId,
                     item.EvidenceRequestId), Kernel.StreamKeyOrder.Instance))
        {
            var request = new EvidenceRequest(tenant, decision.ProgramId, item.EvidenceRequestId);
            var key = RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId, item.EvidenceRequestId);
            var settlementId = RiskActionReviewEffects.ReceiverCommandId(source.EventId,
                nameof(SettleEvidenceRequestActionCompletionUse), key, intent.Header.Episode);
            var settled = await bus.SendAsync(new SettleEvidenceRequestActionCompletionUse(settlementId, canceled,
                request.Stream, intent.Header.RootOperationId, generation, null, intentReference,
                item.EvidenceRequestId, intent.ActionId, decision.ProgramId, SourceUseSettlementDisposition.Canceled),
                context, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Applied(canceled);
    }
}
