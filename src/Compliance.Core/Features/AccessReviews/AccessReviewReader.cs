using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Reads a campaign and its items from their own owner streams and composes them as an
///     <see cref="AccessReviewCampaignSnapshot" />. Every reviewer list, item list, decision, change, verification and
///     exception is verified immutable content; an item the launch fan-out has not materialized yet reads as an
///     unresolved item at revision zero.
/// </summary>
public sealed class AccessReviewReader(IAggregateReader reader, IImmutableContentStore content,
    AccessReviewManifests manifests)
{
    /// <summary>Reads the campaign and every frozen item of it.</summary>
    public ValueTask<AccessReviewCampaignSnapshot> ReadCampaignAsync(Uuid tenantId, Uuid campaignId,
        CancellationToken ct = default) => ReadCampaignAsync(tenantId, campaignId, Uuid.Empty, ct);

    /// <summary>Reads the campaign and every frozen item of it, selecting the logical operation to resolve on the campaign.</summary>
    public async ValueTask<AccessReviewCampaignSnapshot> ReadCampaignAsync(Uuid tenantId, Uuid campaignId,
        Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return new AccessReviewCampaignSnapshot(tenantId, campaign, null, null, []);
        var launch = await LaunchOfAsync(tenantId, launched, ct).ConfigureAwait(false);
        var states = new List<AccessReviewItemState>(launch.Items.Count);
        foreach (var item in launch.Items)
            states.Add(await ItemStateAsync(tenantId, campaignId, item, launched.RemediationOwnerMemberId, ct)
                .ConfigureAwait(false));
        AccessReviewCampaignCompletionView? completion = null;
        if (campaign.Closed is { } closed)
            completion = await AccessReviewContent.ReadAsync<AccessReviewCampaignCompletionView>(content, tenantId,
                closed.CompletionContent, ct).ConfigureAwait(false);
        return new AccessReviewCampaignSnapshot(tenantId, campaign, launch, completion, states);
    }

    /// <summary>Reads the campaign stream and one item of it, selecting the logical operation to resolve on the item.</summary>
    public async ValueTask<AccessReviewItemRead> ReadItemAsync(Uuid tenantId, Uuid campaignId, Uuid itemId,
        Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId), ct)
            .ConfigureAwait(false);
        var item = await reader.HydrateAsync(new AccessReviewItem(tenantId, campaignId, itemId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return new AccessReviewItemRead(campaign, item, null, null);
        AccessReviewItemView? frozen;
        if (item.Opened is not null)
            frozen = await AccessReviewItemFold.FrozenItemAsync(item, content, tenantId, ct).ConfigureAwait(false);
        else
            frozen = (await manifests.ItemsAsync(AccessReviewContent.Realm(tenantId), launched.ItemsManifest, ct)
                .ConfigureAwait(false)).GetValueOrDefault(itemId);
        if (frozen is null)
            return new AccessReviewItemRead(campaign, item, null, null);
        var state = await AccessReviewItemFold.ReadAsync(item, frozen, launched.RemediationOwnerMemberId, content,
            tenantId, ct).ConfigureAwait(false);
        return new AccessReviewItemRead(campaign, item, frozen, state);
    }

    /// <summary>Reads the frozen launch of a launched campaign from its verified content.</summary>
    public async Task<AccessReviewLaunch> LaunchOfAsync(Uuid tenantId, AccessReviewCampaignLaunched launched,
        CancellationToken ct = default)
    {
        var reviewers = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessReviewerView>>(content, tenantId,
            launched.ReviewersManifest, ct).ConfigureAwait(false);
        var items = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessReviewItemView>>(content, tenantId,
            launched.ItemsManifest, ct).ConfigureAwait(false);
        return new AccessReviewLaunch(launched.CampaignId, launched.Name, launched.Instructions, launched.Deadline,
            launched.SnapshotId, launched.ContentSha256, reviewers, items, launched.LaunchedBy, launched.LaunchedAt,
            launched.ProgramId, launched.RemediationOwnerMemberId);
    }

    async Task<AccessReviewItemState> ItemStateAsync(Uuid tenantId, Uuid campaignId, AccessReviewItemView frozen,
        Uuid? remediationOwner, CancellationToken ct)
    {
        var item = await reader.HydrateAsync(new AccessReviewItem(tenantId, campaignId, frozen.ItemId), ct)
            .ConfigureAwait(false);
        return await AccessReviewItemFold.ReadAsync(item, frozen, remediationOwner, content, tenantId, ct)
            .ConfigureAwait(false);
    }
}

/// <summary>A campaign stream and one item of it, with the item's frozen view and folded state when the campaign has the item.</summary>
public sealed record AccessReviewItemRead(AccessReviewCampaign Campaign, AccessReviewItem Item,
    AccessReviewItemView? Frozen, AccessReviewItemState? State);
