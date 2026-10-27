using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Readers;

/// <summary>Projects frozen campaign-to-system links on an independently replayable checkpoint.</summary>
sealed class FitzAccessReviewCampaignImpactDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/access-review-campaign-application-impact-v1/projection",
            AccessReviewCampaignImpactSchema.Projector),
        IAccessReviewCampaignImpactDirectoryReader, IAccessReviewCampaignImpactProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case AccessReviewCampaignLaunched launched:
                await ApplyLaunchedAsync(launched, ct).ConfigureAwait(false);
                break;
            case AccessReviewCampaignCompleted completed:
                await ApplyCompletedAsync(completed, ct).ConfigureAwait(false);
                break;
        }
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(AccessReviewCampaignImpactSchema.Projector,
            AccessReviewCampaignImpactSchema.Pattern(tenantId)), ct);

    public async ValueTask<Page<AccessReviewCampaignSystemInstanceReferenceView>>
        ListBySystemInstanceAsync(Uuid tenantId, Uuid systemInstanceId, int limit,
            string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AccessReviewCampaignImpactSchema.SystemInstanceReferences.QueryAsync(tx,
            AccessReviewCampaignImpactSchema.BySystemInstance.Query()
                .WithPrefix(systemInstanceId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<AccessReviewCampaignSummaryView?> GetCampaignAsync(Uuid tenantId,
        Uuid campaignId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await AccessReviewCampaignImpactSchema.Campaigns.GetAsync(tx, campaignId, ct)
            .ConfigureAwait(false))?.Summary;
    }

    async ValueTask ApplyLaunchedAsync(AccessReviewCampaignLaunched launched,
        CancellationToken ct)
    {
        if (launched.TenantId == Uuid.Empty || launched.CampaignId == Uuid.Empty ||
            launched.SnapshotId == Uuid.Empty || launched.LaunchedAt == default)
            throw new InvalidOperationException("An access review campaign launch is invalid.");
        var launchedItems = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessReviewItemView>>(content,
            launched.TenantId, launched.ItemsManifest, ct).ConfigureAwait(false);

        var summaries = AccessReviewCampaignImpactSchema.Campaigns;
        var existing = await summaries.GetAsync(Transaction, launched.CampaignId, ct)
            .ConfigureAwait(false);
        var summary = new AccessReviewCampaignSummaryView(launched.TenantId,
            launched.CampaignId, launched.Name, launched.Deadline, AccessReviewCampaign.Active,
            launchedItems.Count, launched.SnapshotId, launched.LaunchedAt, null, null);
        var systemInstanceIds = launchedItems.Select(static item => item.SystemInstanceId)
            .Distinct()
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (launched.ContentSha256 is not { Length: 64 } contentSha256 ||
            !contentSha256.All(Uri.IsHexDigit) ||
            systemInstanceIds.Any(static id => id == Uuid.Empty))
            throw new InvalidOperationException("A campaign launch contains invalid impact data.");
        var impactSummary = new AccessReviewCampaignImpactSummaryView(launched.TenantId,
            summary, contentSha256, systemInstanceIds);
        if (existing is null)
            await summaries.InsertAsync(Transaction, impactSummary, ct).ConfigureAwait(false);
        else if (existing.TenantId != impactSummary.TenantId ||
                 existing.Summary.Name != summary.Name ||
                 existing.Summary.Deadline != summary.Deadline ||
                 existing.Summary.ItemCount != summary.ItemCount ||
                 existing.Summary.SnapshotId != summary.SnapshotId ||
                 existing.Summary.LaunchedAt != summary.LaunchedAt ||
                 existing.ContentSha256 != impactSummary.ContentSha256 ||
                 !existing.SystemInstanceIds.SequenceEqual(impactSummary.SystemInstanceIds))
            throw new InvalidOperationException("An access review campaign launch changed on replay.");

        var references = AccessReviewCampaignImpactSchema.SystemInstanceReferences;
        foreach (var systemInstanceId in systemInstanceIds)
        {
            var id = Uuid.CreateVersion5(launched.CampaignId,
                $"system-instance:{systemInstanceId}");
            var reference = new AccessReviewCampaignSystemInstanceReferenceView(
                launched.TenantId, launched.CampaignId, systemInstanceId, launched.LaunchedAt);
            var current = await references.GetAsync(Transaction, id, ct).ConfigureAwait(false);
            if (current is null)
                await references.InsertAsync(Transaction, reference, ct).ConfigureAwait(false);
            else if (current != reference)
                throw new InvalidOperationException(
                    "An access review campaign system reference changed on replay.");
        }
    }

    async ValueTask ApplyCompletedAsync(AccessReviewCampaignCompleted completed,
        CancellationToken ct)
    {
        var summaries = AccessReviewCampaignImpactSchema.Campaigns;
        var existing = await summaries.GetAsync(Transaction, completed.CampaignId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A campaign completion cannot project before its launch.");
        if (existing.TenantId != completed.TenantId)
            throw new InvalidOperationException("An access review campaign completion is invalid.");

        var completion = await AccessReviewContent.ReadAsync<AccessReviewCampaignCompletionView>(content,
            completed.TenantId, completed.CompletionContent, ct).ConfigureAwait(false);
        if (completion.SnapshotId == Uuid.Empty || completion.CompletedAt == default)
            throw new InvalidOperationException("An access review campaign completion is invalid.");
        var updated = existing with
        {
            Summary = existing.Summary with
            {
                Status = AccessReviewCampaign.Completed,
                FinalSnapshotId = completion.SnapshotId,
                CompletedAt = completion.CompletedAt,
            },
        };
        if (existing.Summary.Status == AccessReviewCampaign.Completed)
        {
            if (existing != updated)
                throw new InvalidOperationException(
                    "An access review campaign completion changed on replay.");
            return;
        }
        await summaries.ReplaceAsync(Transaction, existing, updated, ct).ConfigureAwait(false);
    }
}
