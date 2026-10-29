using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Readers;

/// <summary>
///     Reads a campaign and its items from their own owner streams and composes them as an
///     <see cref="AccessReviewCampaignSnapshot" />. Every reviewer list, item list, decision, change, verification and
///     exception is verified immutable content; an item the launch fan-out has not materialized yet reads as an
///     unresolved item at revision zero.
/// </summary>
public sealed class AccessReviewReader(IAggregateReader reader, IImmutableContentStore content,
    AccessReviewManifests manifests, IDomainEventReader events)
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
        return await ReadCampaignAsync(tenantId, campaign, ct).ConfigureAwait(false);
    }

    /// <summary>Reads immutable campaign identity for current authorization before resolving accepted work.</summary>
    public async ValueTask<AccessReviewCampaignSnapshot> ReadCampaignIdentityAsync(Uuid tenantId, Uuid campaignId,
        Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        var history = await AccessReviewCampaignHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var launch = owner.Launched is { } launched
            ? await LaunchOfAsync(tenantId, launched, ct).ConfigureAwait(false) : null;
        return new AccessReviewCampaignSnapshot(tenantId, owner, launch, null, [], history);
    }

    /// <summary>Composes the provided exact owner cut without selecting a newer native campaign head.</summary>
    public async ValueTask<AccessReviewCampaignSnapshot> ReadCampaignAsync(Uuid tenantId, AccessReviewCampaign campaign,
        CancellationToken ct = default)
    {
        var history = await AccessReviewCampaignHistory.ReadAsync(events, campaign, ct).ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return new AccessReviewCampaignSnapshot(tenantId, campaign, null, null, [], history);
        var launch = await LaunchOfAsync(tenantId, launched, ct).ConfigureAwait(false);
        var states = new List<AccessReviewItemState>(launch.Items.Count);
        foreach (var item in launch.Items)
            states.Add(await ItemStateAsync(tenantId, campaign.Id, item, launched.RemediationOwnerMemberId, ct)
                .ConfigureAwait(false));
        AccessReviewCampaignCompletionView? completion = null;
        if (campaign.Closed is { } closed)
            completion = await AccessReviewContent.ReadAsync<AccessReviewCampaignCompletionView>(content, tenantId,
                closed.CompletionContent, ct).ConfigureAwait(false);
        return new AccessReviewCampaignSnapshot(tenantId, campaign, launch, completion, states, history);
    }

    /// <summary>Reads the current item view and full verified business history.</summary>
    public async ValueTask<AccessReviewItemRead> ReadItemAsync(Uuid tenantId, Uuid campaignId, Uuid itemId,
        Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var read = await ReadItemIdentityAsync(tenantId, campaignId, itemId, selectedLogicalId, ct).ConfigureAwait(false);
        if (read.Frozen is null || read.Campaign.Launched is not { } launched)
            return read;
        var history = await AccessReviewItemHistory.ReadAsync(events, read.Item, ct).ConfigureAwait(false);
        var state = await AccessReviewItemFold.ReadAsync(read.Item, history, read.Frozen, launched.RemediationOwnerMemberId,
            content, tenantId, ct).ConfigureAwait(false);
        return read with { State = state, History = history };
    }

    /// <summary>Reads only immutable launch identity for current authorization before accepted retry.</summary>
    public async ValueTask<AccessReviewItemRead> ReadItemIdentityAsync(Uuid tenantId, Uuid campaignId, Uuid itemId,
        Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId), ct).ConfigureAwait(false);
        var item = await reader.HydrateAsync(new AccessReviewItem(tenantId, campaignId, itemId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return new(campaign, item, null, null);
        var frozen = item.Opened is not null
            ? await AccessReviewItemFold.FrozenItemAsync(item, content, tenantId, ct).ConfigureAwait(false)
            : (await manifests.ItemsAsync(AccessReviewContent.Realm(tenantId), launched.ItemsManifest, ct)
                .ConfigureAwait(false)).GetValueOrDefault(itemId);
        return new(campaign, item, frozen, null);
    }

    /// <summary>Folds fresh business state at the actual owner provided by native preparation.</summary>
    public async ValueTask<AccessReviewItemState> ReadStateAsync(Uuid tenantId, AccessReviewItem owner,
        AccessReviewItemView frozen, Uuid? remediationOwner, CancellationToken ct = default)
    {
        if (owner.Opened is not null)
        {
            var actual = await AccessReviewItemFold.FrozenItemAsync(owner, content, tenantId, ct).ConfigureAwait(false);
            if (Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.OfJson(actual) != Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.OfJson(frozen))
                throw new ImmutableContentIntegrityException("The frozen authorization identity differs from the prepared item's actual creation.");
        }
        var history = await AccessReviewItemHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        return await AccessReviewItemFold.ReadAsync(owner, history, frozen, remediationOwner, content, tenantId, ct)
            .ConfigureAwait(false);
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
        var history = await AccessReviewItemHistory.ReadAsync(events, item, ct).ConfigureAwait(false);
        return await AccessReviewItemFold.ReadAsync(item, history, frozen, remediationOwner, content, tenantId, ct)
            .ConfigureAwait(false);
    }
}
