using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     The evidence-request pair of the generic settle receiver. The terminal fact is resolved from the action: a
///     decided review requires <c>irreversible_decision</c>, which releases the pinned request after the historical
///     decision, and a canceled review requires <c>canceled</c>. A missing terminal fact is a transient wait, never an
///     inferred cancellation. The settled requirement is the one the review froze for this request at admission.
/// </summary>
sealed class SettleEvidenceRequestActionCompletionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, RiskActionReviewVerifier verifier)
    : SettleSourceUseHandler<EvidenceRequest, SettleEvidenceRequestActionCompletionUse>(reader, executor, content, clock)
{
    protected override EvidenceRequest Open(SettleEvidenceRequestActionCompletionUse request, Uuid selectedCommandId) =>
        new(Uuid.Parse(request.Target.Realm, System.Globalization.CultureInfo.InvariantCulture), request.ProgramId,
            request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleEvidenceRequestActionCompletionUse request, CancellationToken ct)
    {
        var review = await verifier.VerifyTerminalAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (review.Error is not null)
            return Result<SourceUseSource>.Failure(review.Error);
        var (action, intent, decision, tenant) = (review.Action!, review.Intent!, review.Decision!, review.TenantId);
        var accepted = action.AcceptedReview(request.OperationId)!;
        if (request.ReceivingOwnerId != intent.ActionId || request.ProgramId != decision.ProgramId ||
            request.Generation != accepted.Generation)
            return Fail("The settlement differs from the frozen review intent.");
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(Content, tenant.ToString(),
                intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen review source uses cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var evidence = Open(request, request.CommandId);
        if (uses.FirstOrDefault(use => use.SourceOwner == evidence.Stream) is not { } requirement)
            return Fail("The evidence request is not a frozen source use of this review.");
        var disposition = action.TerminalReview(request.OperationId) switch
        {
            RiskTreatmentActionCompletionReviewed => SourceUseSettlementDisposition.IrreversibleDecision,
            RiskTreatmentActionCompletionReviewCanceled => SourceUseSettlementDisposition.Canceled,
            _ => (SourceUseSettlementDisposition?)null,
        };
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            RiskActionReviewEffects.UseKey(intent, request.Generation, evidence.Stream), requirement,
            nameof(SettleEvidenceRequestActionCompletionUse), disposition, null));
    }

    protected override Func<EvidenceRequest, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (EvidenceRequest request, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            request.SettleActionCompletionUse(provenance, intent, generation);

    static Result<SourceUseSource> Fail(string message, bool transient = false) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
