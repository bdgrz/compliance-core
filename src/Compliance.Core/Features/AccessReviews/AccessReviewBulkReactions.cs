using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>What the cancellation and settlement reactors of a batch read before they dispatch a receiver.</summary>
static class AccessReviewBulkReactions
{
    /// <summary>
    ///     The tenant, the frozen intent and the admission fact a batch fact derives from. A fact that is not visible
    ///     yet is transient; the receivers verify the same admission, so nothing here is trusted on its own.
    /// </summary>
    public static async Task<(Uuid Tenant, BulkAccessDecisionIntent Intent, NativeFactReference Source)> ResolveAsync(
        string realm, ImmutableContentRef intentReference, IImmutableContentStore content, IAggregateReader reader,
        CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        BulkAccessDecisionIntent intent;
        try
        {
            intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified yet: {exception.Message}", true));
        }

        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, intent.CampaignId), ct)
            .ConfigureAwait(false);
        if (campaign.AdmittedFor(intent.DecisionBatchId) is not { } admitted)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "The admitted bulk decision is not visible yet.", true));
        return (tenant, intent, BulkReceiverSupport.Admission(campaign, admitted));
    }

    /// <summary>The selected items of a batch in canonical key order.</summary>
    public static async Task<IReadOnlyList<BulkAccessDecisionItemIntent>> ItemsAsync(AccessReviewManifests manifests,
        string realm, BulkAccessDecisionIntent intent, CancellationToken ct)
    {
        try
        {
            return [.. (await manifests.BulkItemsAsync(realm, intent.Items, ct).ConfigureAwait(false)).Values
                .OrderBy(static item => item.ItemOwner.Resource, Kernel.StreamKeyOrder.Instance)];
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified yet: {exception.Message}", true));
        }
    }
}
