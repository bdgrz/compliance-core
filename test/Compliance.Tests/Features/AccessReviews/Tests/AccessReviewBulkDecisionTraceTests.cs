using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>The design review traces of the bulk decision batch and the campaign completion that races it.</summary>
public sealed class AccessReviewBulkDecisionTraceTests
{
    [Fact]
    public async Task ShouldDrainAdmittedBulkDecisionGivenCampaignClosureRacesMaterialization()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaignId = launched.CampaignId;
        var view = await fixture.CampaignAsync(campaignId);
        foreach (var item in view.Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(campaignId, item.Item.ItemId, 1, "keep",
                "Still needed."));
        var first = AccessReviewCampaignTests.ItemId(view, "bot", "deploy");
        var second = AccessReviewCampaignTests.ItemId(view, "legacy", "read");
        IReadOnlyList<Uuid> selected = [first, second];

        // Act: step 1, the complete preview is admitted on the campaign (revision 1 to 2); the batch does not exist yet.
        var (request, acknowledgment) = await fixture.AdmitBulkAsync(fixture.ManagerUserId, campaignId, selected,
            "keep", "Bulk reaffirmation.");
        var admittedCampaign = await fixture.CampaignAggregateAsync(campaignId);
        var admitted = Assert.IsType<AccessReviewDecisionBatchAdmitted>((await fixture.CampaignHistoryAsync(admittedCampaign)).Facts[^1]);
        var beforeBatch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Act: step 2, the completion is requested at revision 2 while child creation is paused.
        var completion = fixture.Completion(campaignId, 2);
        await fixture.SendAsync(fixture.ManagerUserId, completion);
        var racing = await fixture.RunCompletionAsync(campaignId, completion.CommandId);
        var racingCampaign = await fixture.CampaignAggregateAsync(campaignId);
        var heldWhileRacing = new List<DomainEvent>();
        foreach (var itemId in selected)
            heldWhileRacing.AddRange((await AccessReviewCompletionUses(fixture, campaignId, itemId)));
        var sealedOut = await fixture.FailAsync(fixture.ManagerUserId, fixture.BulkRequest(campaignId, selected,
            request.PreviewToken, 3, rationale: "Too late."), RequestErrorKind.Conflict);

        // Act: step 3, the batch materializes, reserves both items and appends the first provisional decision, then
        // the worker crashes; the unpublished decision is hidden and a source replay restores the batch.
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await AccessReviewBulkDecisionTests.ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        foreach (var target in record.Targets)
            await AccessReviewBulkDecisionTests.ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Reserve,
                nameof(ReserveAccessReviewItemDecision), target);
        await AccessReviewBulkDecisionTests.ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Apply,
            nameof(ApplyAccessReviewItemDecision), record.Targets[0]);
        var crashed = await fixture.CampaignAsync(campaignId);
        var stillWaiting = await fixture.RunCompletionAsync(campaignId, completion.CommandId);

        // Act: step 4, the replay applies the second item and publishes the exact full proof set; both become visible.
        var resumed = await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);
        var published = await fixture.CampaignAsync(campaignId);
        var finished = await fixture.RunCompletionAsync(campaignId, completion.CommandId);
        var closedCampaign = await fixture.CampaignAggregateAsync(campaignId);
        var closed = Assert.IsType<AccessReviewCampaignCompleted>((await fixture.CampaignHistoryAsync(closedCampaign)).Facts[^1]);
        var proofs = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<IReadOnlyList<AccessReviewItemCompletionProof>>(fixture.TenantId.ToString(),
                closed.ItemCompletionProofs);

        // Act: step 5, the retry after completion.
        var retried = await fixture.SendAsync(fixture.ManagerUserId, request);
        var after = await fixture.CampaignAggregateAsync(campaignId);
        var afterBatch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Assert: step 1
        Assert.Equal(request.DecisionBatchId, acknowledgment.OperationId);
        Assert.Equal(2, admittedCampaign.Revision);
        Assert.Equal(1UL, admitted.Operation.OriginalExpectedRevision);
        Assert.False(beforeBatch.IsMaterialized);

        // Assert: step 2
        Assert.Equal(ProcessState.Pending, racing.State);
        Assert.Contains("has not drained", racing.Note, StringComparison.Ordinal);
        Assert.False(racingCampaign.IsCompleted);
        Assert.Equal(3, racingCampaign.Revision);
        Assert.Empty(heldWhileRacing);
        Assert.Contains("completion", sealedOut.Message, StringComparison.Ordinal);

        // Assert: step 3
        Assert.All(crashed.Items.Where(item => selected.Contains(item.Item.ItemId)),
            item => Assert.Equal("Still needed.", item.Decision!.Rationale));
        Assert.Equal(ProcessState.Pending, stillWaiting.State);

        // Assert: step 4
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(0, batch.Accepted!.Revision);
        Assert.NotNull(batch.Published);
        Assert.All(published.Items.Where(item => selected.Contains(item.Item.ItemId)), item =>
        {
            Assert.Equal("Bulk reaffirmation.", item.Decision!.Rationale);
            Assert.Equal(3, item.Revision);
        });
        Assert.Equal(ProcessState.Completed, finished.State);
        Assert.True(closedCampaign.IsCompleted);
        Assert.Equal(4, closedCampaign.Revision);
        Assert.Equal(view.Items.Count, proofs.Count);
        Assert.All(proofs.Where(proof => selected.Contains(proof.ItemId)), proof =>
        {
            Assert.Equal(2UL, proof.Requirement.ResourceRevision);
            Assert.Equal(3, proof.Facts.Revision);
        });

        // Assert: step 5
        Assert.Equal(acknowledgment, retried);
        Assert.Equal((await fixture.CampaignHistoryAsync(closedCampaign)).Facts.Count, (await fixture.CampaignHistoryAsync(after)).Facts.Count);
        Assert.Equal(batch.Facts.Count, afterBatch.Facts.Count);
    }

    static async Task<List<DomainEvent>> AccessReviewCompletionUses(AccessReviewFixture fixture, Uuid campaignId,
        Uuid itemId)
    {
        var item = await fixture.ItemAsync(campaignId, itemId);
        return [.. item.Uses.ActiveUses.Select(static use => use.GrantFact!)
            .Where(static fact => fact is AccessReviewItemCampaignCompletionUseAuthorized)];
    }
}
