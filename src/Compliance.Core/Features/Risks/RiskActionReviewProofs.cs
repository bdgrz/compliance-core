using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Derives the held fulfilment proofs of a review from the authoritative evidence request owners.</summary>
static class RiskActionReviewProofs
{
    /// <summary>
    ///     The held fulfilment proof of every cited evidence request in canonical key order, or the reason one is not held.
    ///     A grant that is not visible yet is transient; a request that is held but not fulfilled is permanent.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<EvidenceUseProof>>> HeldAsync(IAggregateReader reader,
        IImmutableContentStore content, RiskActionReview review, long generation, Uuid commandId, CancellationToken ct)
    {
        var (intent, decision, tenant) = (review.Intent!, review.Decision!, review.TenantId);
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        try
        {
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, tenant.ToString(),
                intent.CompletionEvidence, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Failure($"The cited evidence manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var proofs = new List<EvidenceUseProof>();
        foreach (var item in cited.OrderBy(item => RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId,
                     item.EvidenceRequestId), Kernel.StreamKeyOrder.Instance))
        {
            var request = await reader.HydrateAsync(new EvidenceRequest(tenant, decision.ProgramId,
                item.EvidenceRequestId, commandId), ct).ConfigureAwait(false);
            if (request.Uses.Find(RiskActionReviewEffects.UseKey(intent, generation, request.Stream)) is not
                { GrantFact: { } grant, IsActive: true })
                return Failure("An evidence request is not held by this review yet.", true);
            if (request.FulfilledFact is not { } fulfilled)
                return Failure("A held evidence request is not fulfilled.", false);
            proofs.Add(new EvidenceUseProof(item.EvidenceRequestId, NativeFactReference.Of(request.Stream, fulfilled),
                NativeFactReference.Of(request.Stream, grant)));
        }

        return Result<IReadOnlyList<EvidenceUseProof>>.Success(proofs);
    }

    static Result<IReadOnlyList<EvidenceUseProof>> Failure(string message, bool transient) =>
        Result<IReadOnlyList<EvidenceUseProof>>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
