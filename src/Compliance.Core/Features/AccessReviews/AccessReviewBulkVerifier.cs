using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Verifies a receiver's proven source: the frozen bulk intent is read from verified immutable content and the
///     admitted batch fact is found on the authoritative campaign. The source reference only names the fact; nothing a
///     command states is trusted until the campaign confirms it.
/// </summary>
sealed class AccessReviewBulkVerifier(IAggregateReader reader, IImmutableContentStore content,
    AccessReviewManifests manifests)
{
    public async ValueTask<AccessReviewBulk> VerifyAsync(Uuid tenantId, Uuid campaignId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        BulkAccessDecisionIntent intent;
        try
        {
            intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenantId, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return AccessReviewBulk.Failed(Transient($"The frozen bulk decision is not readable yet: {exception.Message}"));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return AccessReviewBulk.Failed(Permanent($"The frozen bulk decision cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.DecisionBatchId != operationId ||
            intent.CampaignId != campaignId || intent.CampaignGeneration != (ulong)AccessReviewEffects.Generation)
            return AccessReviewBulk.Failed(Permanent("The command differs from the frozen bulk decision intent."));
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedRootId: operationId), ct)
            .ConfigureAwait(false);
        if (campaign.AdmittedFor(operationId) is not { } admitted)
            return AccessReviewBulk.Failed(Transient("The admitted bulk decision is not visible yet."));
        if (source.Stream != campaign.Stream || admitted.Metadata!.EventId != source.EventId ||
            admitted.Metadata.AggregateVersion != source.AggregateVersion || admitted.Intent != intentReference)
            return AccessReviewBulk.Failed(Permanent("The source is not the admitted bulk decision of this campaign."));
        try
        {
            AccessReviewIntentBindings.Require(intent.Header, admitted.Operation);
            var items = await manifests.BulkItemsAsync(AccessReviewContent.Realm(tenantId), intent.Items, ct)
                .ConfigureAwait(false);
            if (items.Count == 0 || intent.ExpectedCampaignRevision != admitted.Operation.OriginalExpectedRevision ||
                items.Values.Any(item => item.ItemId == Uuid.Empty || item.ReviewerMemberId == Uuid.Empty ||
                    item.ExpectedItemRevision == 0 || item.ItemOwner != AccessReviewEffects.ItemStream(tenantId, campaignId, item.ItemId)))
                throw new ImmutableContentIntegrityException("The complete bulk selection differs from its admitted scoped item owners.");
        }
        catch (ImmutableContentIntegrityException error)
        {
            return AccessReviewBulk.Failed(new RequestError(RequestErrorKind.Conflict, error.Message,
                error is ImmutableContentUnavailableException));
        }
        return new AccessReviewBulk(campaign, admitted, intent, null);
    }

    /// <summary>The frozen item intent of a selected item, or null when the batch does not select it.</summary>
    public async ValueTask<BulkAccessDecisionItemIntent?> ItemOfAsync(string realm, BulkAccessDecisionIntent intent,
        string itemResource, CancellationToken ct) => (await manifests.BulkItemsAsync(realm, intent.Items, ct)
        .ConfigureAwait(false)).GetValueOrDefault(itemResource);

    /// <summary>The exact head the preview froze for an item.</summary>
    public async ValueTask<NativeFactReference> HeadOfAsync(Uuid tenantId, BulkAccessDecisionItemIntent item,
        CancellationToken ct)
    {
        var proofs = await ImmutableSelectionManifests.ReadAsync<BulkItemEligibilityProof>(content,
            AccessReviewContent.Realm(tenantId), item.EligibilityProofs, ct).ConfigureAwait(false);
        if (item.EligibilityProofs.ItemSchema != BulkItemEligibilityProof.Schema ||
            item.EligibilityProofs.Order != SelectionOrder.CanonicalKey)
            throw new ImmutableContentIntegrityException("The eligibility proof has another schema or order contract.");
        var head = Assert(proofs).ItemHead;
        if (head.Stream != item.ItemOwner || head.EventId == Uuid.Empty || head.AggregateVersion == 0)
            throw new ImmutableContentIntegrityException("The eligibility head belongs to another exact item source.");
        return head;
    }

    static BulkItemEligibilityProof Assert(IReadOnlyList<BulkItemEligibilityProof> proofs) => proofs.Count == 1
        ? proofs[0]
        : throw new ImmutableContentIntegrityException("A selected item freezes exactly one eligibility proof.");

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}

/// <summary>A verified bulk decision source, or the error that explains why it is not provable.</summary>
sealed record AccessReviewBulk(AccessReviewCampaign? Campaign, AccessReviewDecisionBatchAdmitted? Admitted,
    BulkAccessDecisionIntent? Intent, RequestError? Error)
{
    public static AccessReviewBulk Failed(RequestError error) => new(null, null, null, error);
}
