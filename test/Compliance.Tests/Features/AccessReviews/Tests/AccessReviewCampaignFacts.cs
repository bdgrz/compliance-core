using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
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
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>Builds access review launch and completion facts with their verified content for projector and baseline tests.</summary>
static class AccessReviewCampaignFacts
{
    public static IImmutableContentStore NewStore() =>
        new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));

    public static OperationProvenance Provenance(Uuid tenantId, Uuid campaignId, Uuid logicalId, string kind,
        ActorReference actor, DateTimeOffset at) => new(logicalId, logicalId, null, kind, actor,
        AccessReviewEffects.CampaignStream(tenantId, campaignId), "initial", null, new string('d', 64), at,
        AccessReviewEffects.CampaignStream(tenantId, campaignId), AccessReviewEffects.CampaignStream(tenantId, campaignId),
        AccessReviewEffects.CampaignStream(tenantId, campaignId));

    public static async Task<AccessReviewCampaignLaunched> LaunchedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid campaignId, string name, DateTimeOffset deadline, Uuid snapshotId, string contentSha256,
        IReadOnlyList<AccessReviewerView> reviewers, IReadOnlyList<AccessReviewItemView> items, ActorReference actor,
        DateTimeOffset launchedAt, Uuid? programId = null, Uuid? remediationOwnerMemberId = null,
        string instructions = "Review")
    {
        var realm = tenantId.ToString();
        var reviewersRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            AccessReviewContent.ReviewersSchema, reviewers);
        var itemsRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewContent.ItemsSchema, items);
        var manifest = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemIntentManifest.Schema,
            new AccessReviewItemIntentManifest([]));
        var (response, result) = await AccessReviewContent.FreezeOkAsync(content, tenantId,
            AccessReviewContent.RegistrationSchema, new AccessReviewCampaignRegistration(campaignId, snapshotId,
                contentSha256, items.Count), CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, realm, response);
        return new AccessReviewCampaignLaunched(tenantId, campaignId, name, instructions, deadline, snapshotId,
            contentSha256, reviewersRef, itemsRef, manifest, actor, launchedAt, programId, remediationOwnerMemberId,
            Provenance(tenantId, campaignId, campaignId, "LaunchAccessReviewCampaign", actor, launchedAt), first, result);
    }

    public static async Task<AccessReviewCampaignCompleted> CompletedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid campaignId, long revision, AccessReviewCampaignCompletionView completion, ActorReference actor)
    {
        var realm = tenantId.ToString();
        var completionRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            AccessReviewContent.CompletionSchema, completion);
        var proofs = await content.WriteJsonAsync<IReadOnlyList<AccessReviewItemCompletionProof>>(realm,
            Uuid.CreateVersion4(), AccessReviewContent.ItemProofsSchema, []);
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ClosureIntent.Schema, new UnitResult());
        var result = await AccessReviewContent.WriteResultAsync(content, tenantId,
            AccessReviewContent.CompletionViewSchema, completionRef, CancellationToken.None);
        return new AccessReviewCampaignCompleted(tenantId, campaignId, revision, completionRef,
            Provenance(tenantId, campaignId, Uuid.CreateVersion4(), "CompleteAccessReviewCampaignClosure", actor,
                completion.CompletedAt), 1, proofs, intent, null, result);
    }

    /// <summary>
    ///     Saves a launched campaign and its materialized items through the real executor, so every fact carries
    ///     committed metadata. Items are materialized unless <paramref name="materialize" /> is false.
    /// </summary>
    public static async Task SeedLaunchedAsync(IServiceProvider provider, Uuid tenantId, Uuid campaignId,
        IReadOnlyList<AccessReviewerView> reviewers, IReadOnlyList<AccessReviewItemView> items, ActorReference actor,
        DateTimeOffset now, Uuid? programId = null, Uuid? remediationOwnerMemberId = null,
        string name = "Payroll review", bool materialize = true,
        DateTimeOffset? deadline = null)
    {
        var content = provider.GetRequiredService<IImmutableContentStore>();
        var realm = tenantId.ToString();
        var stream = AccessReviewEffects.CampaignStream(tenantId, campaignId);
        var provenance = Provenance(tenantId, campaignId, campaignId, "LaunchAccessReviewCampaign", actor, now);
        var header = new DurableIntentHeader(campaignId, stream, stream, stream, actor, "initial", null,
            provenance.AuthoredIntentSha256, now);
        var proof = new NativeFactReference(new EventStreamAddress(realm, "population-snapshots",
            campaignId.ToString()), Uuid.CreateVersion4(), 1);
        var selections = new List<AccessReviewItemSelection>();
        foreach (var item in items.OrderBy(static item => item.ItemId.ToString(), StringComparer.Ordinal))
        {
            var entry = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
                AccessReviewContent.ItemEntrySchema, item);
            var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
                AccessReviewItemCreationIntent.Schema, new AccessReviewItemCreationIntent(header, campaignId, 1,
                    item.ItemId, item.SystemInstanceId, entry, item.ReviewerMemberId,
                    AccessReviewEffects.ReviewerEpisode(campaignId, item.ItemId), proof));
            selections.Add(new AccessReviewItemSelection(item.ItemId, intent));
        }

        var reviewersRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            AccessReviewContent.ReviewersSchema, reviewers);
        var itemsRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewContent.ItemsSchema, items);
        var manifest = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AccessReviewItemIntentManifest.Schema,
            new AccessReviewItemIntentManifest(selections));
        var (response, result) = await AccessReviewContent.FreezeOkAsync(content, tenantId,
            AccessReviewContent.RegistrationSchema, new AccessReviewCampaignRegistration(campaignId,
                Uuid.CreateVersion4(), new string('a', 64), items.Count), CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, realm, response);
        await ProgramManagementServices.SeedAsync(provider, new AccessReviewCampaign(tenantId, campaignId, campaignId),
            campaign => campaign.Launch(provenance, name, "Review", deadline ?? now.AddDays(7), Uuid.CreateVersion4(),
                new string('a', 64), reviewersRef, itemsRef, manifest, actor, now, programId, remediationOwnerMemberId,
                first, result));
        if (!materialize)
            return;
        var launch = (await ProgramManagementServices.HydrateAsync(provider,
            new AccessReviewCampaign(tenantId, campaignId))).Launched!;
        foreach (var selection in selections)
        {
            var itemStream = AccessReviewEffects.ItemStream(tenantId, campaignId, selection.ItemId);
            var opened = new OperationProvenance(Uuid.CreateVersion4(), campaignId, launch.Metadata.EventId,
                nameof(MaterializeAccessReviewItem), actor, itemStream, "initial", null, provenance.AuthoredIntentSha256,
                now, itemStream, itemStream, stream);
            await ProgramManagementServices.SeedAsync(provider,
                new AccessReviewItem(tenantId, campaignId, selection.ItemId),
                item => item.Materialize(opened, selection.Intent));
        }
    }
}
