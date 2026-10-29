using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Reactors;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     The bulk decision: the complete preview and selection is admitted on the campaign, the independent batch holds
///     every selected item at the head the preview froze, applies the shared decision provisionally and publishes all of
///     it as one fact. Nothing is visible before the publication and a changed item cancels and fences the whole batch.
/// </summary>
public sealed class AccessReviewBulkDecisionTests
{
    [Fact]
    public async Task ShouldAdmitCompleteSelectionOnCampaignGivenExactPreview()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);

        // Act
        var (request, acknowledgment) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var admitted = Assert.IsType<AccessReviewDecisionBatchAdmitted>((await fixture.CampaignHistoryAsync(campaign)).Facts[^1]);
        var intent = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<BulkAccessDecisionIntent>(fixture.TenantId.ToString(), admitted.Intent);
        var items = await fixture.Provider.GetRequiredService<AccessReviewManifests>()
            .BulkItemsAsync(fixture.TenantId.ToString(), intent.Items);
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);
        var view = await fixture.CampaignAsync(campaignId);

        // Assert
        Assert.Equal(request.DecisionBatchId, acknowledgment.OperationId);
        Assert.Equal(2, campaign.Revision);
        Assert.Equal(1UL, admitted.Operation.OriginalExpectedRevision);
        Assert.Equal(request.DecisionBatchId, admitted.Operation.RootOperationId);
        Assert.NotNull(admitted.FirstResponse);
        Assert.Equal(request.PreviewToken, intent.PreviewToken);
        Assert.Equal("Still needed.", intent.Rationale);
        Assert.Equal(selected.Count, items.Count);
        Assert.All(items.Values, item => Assert.Equal(1UL, item.ExpectedItemRevision));
        Assert.Equal(items.Keys.Order(StreamKeyOrder.Instance), items.Select(static pair => pair.Key));
        Assert.False(batch.IsMaterialized);
        Assert.All(view.Items, item => Assert.Equal("unresolved", item.Status));
    }

    [Fact]
    public async Task ShouldPublishEveryDecisionAtOnceGivenBatchRunsToCompletion()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected, "revoke",
            "No longer needed.");

        // Act
        var process = await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var admitted = (await fixture.CampaignHistoryAsync(campaign)).AdmittedFor(request.DecisionBatchId)!;
        var proofs = await fixture.Provider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<IReadOnlyList<BulkItemDecisionProof>>(fixture.TenantId.ToString(), batch.Published!.SourceProofs!);
        var view = await fixture.CampaignAsync(campaignId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(0, batch.Accepted!.Revision);
        Assert.Equal(1, batch.Revision);
        Assert.Equal(2, campaign.Revision);
        Assert.Equal(admitted.Metadata!.EventId, batch.Accepted.Operation.CausationEventId);
        Assert.Equal(request.DecisionBatchId, batch.Published.Operation.RootOperationId);
        Assert.Equal(selected.Count, proofs.Count);
        Assert.NotNull(batch.Published.OperationResult);
        foreach (var itemId in selected)
        {
            var item = Assert.Single(view.Items, candidate => candidate.Item.ItemId == itemId);
            var owner = await fixture.ItemAsync(campaignId, itemId);
            Assert.Equal("decided", item.Status);
            Assert.Equal(2, item.Revision);
            Assert.Equal("revoke", item.Decision!.Decision);
            Assert.Equal("No longer needed.", item.Decision.Rationale);
            Assert.Equal(request.PreviewToken, item.Decision.BulkPreviewToken);
            Assert.Equal(fixture.ReviewerMemberId, item.Decision.ReviewerMemberId);
            Assert.True((await fixture.ItemHistoryAsync(owner)).IsPublished(request.DecisionBatchId));
            Assert.False(owner.Uses.HasActiveUse);
            Assert.Contains(proofs, proof => proof.ItemId == itemId);
        }

        Assert.Equal(selected.Count, view.Items.Where(item => selected.Contains(item.Item.ItemId))
            .Select(static item => item.Decision!.DecisionId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldKeepDecisionsHiddenGivenPartialApplyUntilOnePublication()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        var batchId = request.DecisionBatchId;

        // Act
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        foreach (var target in record.Targets)
            await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Reserve,
                nameof(ReserveAccessReviewItemDecision), target);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Apply,
            nameof(ApplyAccessReviewItemDecision), record.Targets[0]);
        var partial = await fixture.CampaignAsync(campaignId);
        var firstItem = await fixture.ItemAsync(campaignId, ItemOf(record.Targets[0]));
        var notYet = await Assert.ThrowsAsync<SourceFactUnavailableException>(() => fixture.BulkStepAsync(record,
            BulkAccessDecisionProcess.Publish, nameof(PublishAccessReviewDecisionBatch)));
        foreach (var target in record.Targets.Skip(1))
            await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Apply,
                nameof(ApplyAccessReviewItemDecision), target);
        var beforePublication = await fixture.CampaignAsync(campaignId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Publish,
            nameof(PublishAccessReviewDecisionBatch), null);
        var published = await fixture.CampaignAsync(campaignId);
        foreach (var target in record.Targets)
            await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Settle,
                nameof(SettleAccessReviewItemDecision), target);
        var settled = await fixture.CampaignAsync(campaignId);

        // Assert
        Assert.All(partial.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.NotNull((await fixture.ItemHistoryAsync(firstItem)).ProvisionalFor(batchId));
        Assert.Contains("not visible yet", notYet.Message, StringComparison.Ordinal);
        Assert.All(beforePublication.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.All(published.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.All(settled.Items.Where(item => selected.Contains(item.Item.ItemId)),
            item => Assert.Equal("decided", item.Status));
        Assert.All(settled.Items.Where(item => !selected.Contains(item.Item.ItemId)),
            item => Assert.Equal("unresolved", item.Status));
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentGivenExactRetryAfterPublication()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, first) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        var before = await fixture.CampaignAggregateAsync(campaignId);

        // Act
        var retried = await fixture.SendAsync(fixture.ReviewerUserId, request);
        var rationale = await fixture.FailAsync(fixture.ReviewerUserId, request with { Rationale = "A different rationale." },
            RequestErrorKind.Conflict);
        var revision = await fixture.FailAsync(fixture.ReviewerUserId, request with { ExpectedCampaignRevision = 0 },
            RequestErrorKind.Conflict);
        var items = await fixture.FailAsync(fixture.ReviewerUserId, request with { ItemIds = [.. selected.Skip(1)] },
            RequestErrorKind.Conflict);
        var after = await fixture.CampaignAggregateAsync(campaignId);

        // Assert
        Assert.Equal(first, retried);
        Assert.Contains("different", rationale.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(revision);
        Assert.NotNull(items);
        Assert.Equal((await fixture.CampaignHistoryAsync(before)).Facts.Count, (await fixture.CampaignHistoryAsync(after)).Facts.Count);
        Assert.Equal(1, (await fixture.BatchAsync(campaignId, request.DecisionBatchId)).Revision);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgmentGivenReviewerReassignedAfterAdmission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, first) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        fixture.Permissions.Deny(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);
        foreach (var itemId in selected)
            await fixture.SendAsync(fixture.ManagerUserId, new ReassignAccessReviewResponsibility(
                Uuid.CreateVersion4(), fixture.TenantId, campaignId, itemId, AccessReviewResponsibilityKind.Reviewer,
                1, fixture.ApproverMemberId, "The original reviewer lost queue access.",
                "An independent reviewer is delegated for this system."));
        fixture.Permissions.Restore(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);
        var before = await fixture.CampaignAggregateAsync(campaignId);

        // Act
        var replay = await fixture.SendAsync(fixture.ReviewerUserId, request);

        // Assert
        Assert.Equal(first, replay);
        Assert.Equal((await fixture.CampaignHistoryAsync(before)).Facts.Count, (await fixture.CampaignHistoryAsync(campaignId)).Facts.Count);
    }

    [Fact]
    public async Task ShouldRequirePreviewTokenAndExcludePrivilegedGivenBulkDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.PrivilegedEntitlement,
            new AccessExpectationParameters(EntitlementKind: "admin_privilege"));
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var adaAdmin = AccessReviewCampaignTests.ItemId(campaign, "ada", "admin");
        var botDeploy = AccessReviewCampaignTests.ItemId(campaign, "bot", "deploy");
        var legacyRead = AccessReviewCampaignTests.ItemId(campaign, "legacy", "read");
        var revision = (await fixture.CampaignAggregateAsync(launched.CampaignId)).Revision;

        // Act
        var preview = await fixture.SendAsync(fixture.ReviewerUserId, new PreviewBulkAccessDecision(
            fixture.TenantId, launched.CampaignId, [adaAdmin, botDeploy, legacyRead], "keep"));
        var forged = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(launched.CampaignId,
            [botDeploy, legacyRead], new string('0', 64), revision), RequestErrorKind.Conflict);
        var withPrivileged = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(launched.CampaignId,
            [adaAdmin, botDeploy, legacyRead], preview.PreviewToken, revision), RequestErrorKind.Conflict);
        var recorded = fixture.BulkRequest(launched.CampaignId, [botDeploy, legacyRead], preview.PreviewToken, revision);
        await fixture.SendAsync(fixture.ReviewerUserId, recorded);
        await fixture.RunBulkAsync(launched.CampaignId, recorded.DecisionBatchId);
        var stale = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(launched.CampaignId,
            [botDeploy, legacyRead], preview.PreviewToken, revision, rationale: "Again."), RequestErrorKind.Conflict);
        var decided = await fixture.CampaignAsync(launched.CampaignId);

        // Assert
        Assert.Equal("privileged_requires_individual_decision", Assert.Single(preview.Rejected).Reason);
        Assert.Equal(2, preview.Eligible.Count);
        Assert.Contains("preview", forged.Message, StringComparison.Ordinal);
        Assert.Contains("preview", withPrivileged.Message, StringComparison.Ordinal);
        Assert.Contains("revision", stale.Message, StringComparison.OrdinalIgnoreCase);
        var decisions = decided.Items.Where(item => item.Item.ItemId == botDeploy || item.Item.ItemId == legacyRead)
            .Select(static item => item.Decision!).ToArray();
        Assert.Equal(2, decisions.Length);
        Assert.All(decisions, decision => Assert.Equal(preview.PreviewToken, decision.BulkPreviewToken));
        Assert.Single(decisions.Select(static decision => decision.Rationale).Distinct());
        Assert.Equal("unresolved", Assert.Single(decided.Items, item => item.Item.ItemId == adaAdmin).Status);
    }

    [Fact]
    public async Task ShouldRejectGivenDuplicateOrPartialSelectionOrStaleCampaignRevision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var preview = await fixture.SendAsync(fixture.ReviewerUserId,
            new PreviewBulkAccessDecision(fixture.TenantId, campaignId, selected, "keep"));
        var revision = (await fixture.CampaignAggregateAsync(campaignId)).Revision;

        // Act
        var duplicate = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(campaignId,
            [.. selected, selected[0]], preview.PreviewToken, revision), RequestErrorKind.Conflict);
        var partial = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(campaignId,
            [.. selected.Skip(1)], preview.PreviewToken, revision), RequestErrorKind.Conflict);
        var stale = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(campaignId, selected,
            preview.PreviewToken, revision + 1), RequestErrorKind.Conflict);
        var unbounded = await fixture.FailAsync(fixture.ReviewerUserId, fixture.BulkRequest(campaignId, selected,
            preview.PreviewToken, revision, rationale: new string('x', 4001)), RequestErrorKind.Validation);
        var outsider = await fixture.FailAsync(fixture.ManagerUserId, fixture.BulkRequest(campaignId, selected,
            preview.PreviewToken, revision), RequestErrorKind.NotFound);
        var campaign = await fixture.CampaignAggregateAsync(campaignId);

        // Assert
        Assert.Contains("preview", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("preview", partial.Message, StringComparison.Ordinal);
        Assert.Contains("revision", stale.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rationale", unbounded.Message, StringComparison.Ordinal);
        Assert.NotNull(outsider);
        Assert.Single((await fixture.CampaignHistoryAsync(campaign)).Facts);
    }

    [Fact]
    public async Task ShouldCancelAndFenceBatchGivenItemChangedBetweenPreviewAndHold()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var changed = selected[^1];
        var single = await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, changed, 1, "modify",
            "I reread the evidence first."));

        // Act
        var run = await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        await RunCancellationReactorsAsync(fixture, campaignId, request.DecisionBatchId);
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);
        var view = await fixture.CampaignAsync(campaignId);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Contains("superseded", run.Note, StringComparison.Ordinal);
        Assert.NotNull(batch.CancellationAccepted);
        Assert.NotNull(batch.Canceled);
        Assert.Null(batch.Published);
        Assert.Equal(2, batch.Revision);
        Assert.Equal("modify", single.Decision);
        foreach (var itemId in selected)
        {
            var item = await fixture.ItemAsync(campaignId, itemId);
            var settled = (await UseFactsAsync(fixture, campaignId, itemId)).OfType<AccessReviewItemDecisionSettled>()
                .ToArray();
            Assert.True((await fixture.ItemHistoryAsync(item)).IsFenced(request.DecisionBatchId));
            Assert.False(item.Uses.HasActiveUse);
            Assert.Null((await fixture.ItemHistoryAsync(item)).ProvisionalFor(request.DecisionBatchId));
            Assert.All(settled, fact => Assert.Equal(SourceUseSettlementDisposition.Canceled, fact.Disposition));
            Assert.Single(settled);
        }

        Assert.Equal("modify", Assert.Single(view.Items, item => item.Item.ItemId == changed).Decision!.Decision);
        Assert.All(view.Items.Where(item => item.Item.ItemId != changed), item => Assert.Equal("unresolved", item.Status));
    }

    [Fact]
    public async Task ShouldRefuseDelayedHoldAndApplyGivenFenceBeforeAnyHold()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        var target = record.Targets[0];
        var reserve = await fixture.BulkStepAsync(record, BulkAccessDecisionProcess.Reserve,
            nameof(ReserveAccessReviewItemDecision), target);
        var apply = await fixture.BulkStepAsync(record, BulkAccessDecisionProcess.Apply,
            nameof(ApplyAccessReviewItemDecision), target);
        var abort = await fixture.BulkStepAsync(record with { State = ProcessState.Aborting }, "abort",
            nameof(CancelAccessReviewDecisionBatch));

        // Act
        var cancelled = await fixture.ReceiveAsync(abort);
        await RunCancellationReactorsAsync(fixture, campaignId, request.DecisionBatchId);
        var delayedReserve = await fixture.ReceiveAsync(reserve);
        var delayedApply = await fixture.ReceiveAsync(apply);
        var uses = await UseFactsAsync(fixture, campaignId, ItemOf(target));

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(cancelled.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(delayedReserve.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(delayedApply.Value);
        Assert.Empty(uses.OfType<AccessReviewItemDecisionAuthorized>());
        Assert.Single(uses.OfType<AccessReviewItemDecisionSettled>());
        Assert.Null((await fixture.ItemHistoryAsync(campaignId, ItemOf(target))).ProvisionalFor(request.DecisionBatchId));
    }

    [Fact]
    public async Task ShouldRefuseItemChangeGivenBatchHoldsItAndAllowItAfterCancellation()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        var target = record.Targets[0];
        var itemId = ItemOf(target);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Reserve,
            nameof(ReserveAccessReviewItemDecision), target);

        // Act
        var refused = await fixture.FailAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, itemId, 1, "keep",
            "Racing the batch."), RequestErrorKind.Conflict);
        await ReceiveAppliedAsync(fixture, record with { State = ProcessState.Aborting }, "abort",
            nameof(CancelAccessReviewDecisionBatch), null);
        await RunCancellationReactorsAsync(fixture, campaignId, request.DecisionBatchId);
        var allowed = await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, itemId, 1, "keep",
            "Now allowed."));

        // Assert
        Assert.True(refused.IsTransient);
        Assert.Contains("holds this item", refused.Message, StringComparison.Ordinal);
        Assert.Equal("keep", allowed.Decision);
    }

    [Fact]
    public async Task ShouldRecoverForwardGivenCancelAfterPublication()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var process = await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        var abort = await fixture.BulkStepAsync(process with { State = ProcessState.Aborting }, "abort",
            nameof(CancelAccessReviewDecisionBatch));

        // Act
        var late = await fixture.ReceiveAsync(abort);
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Assert
        var superseded = Assert.IsType<ReceiverOutcome.Superseded>(late.Value);
        Assert.Contains("recover forward", superseded.Reason, StringComparison.Ordinal);
        Assert.NotNull(batch.Published);
        Assert.Null(batch.CancellationAccepted);
    }

    [Fact]
    public async Task ShouldRefusePublicationGivenIncompleteOrFencedProofSet()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        foreach (var target in record.Targets)
            await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Reserve,
                nameof(ReserveAccessReviewItemDecision), target);
        foreach (var target in record.Targets)
            await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Apply,
                nameof(ApplyAccessReviewItemDecision), target);
        var publish = (PublishAccessReviewDecisionBatch)await fixture.BulkStepAsync(record,
            BulkAccessDecisionProcess.Publish, nameof(PublishAccessReviewDecisionBatch));
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var complete = await content.ReadJsonAsync<IReadOnlyList<BulkItemDecisionProof>>(fixture.TenantId.ToString(),
            publish.ItemDecisionProofs);
        var partial = await content.WriteJsonAsync<IReadOnlyList<BulkItemDecisionProof>>(fixture.TenantId.ToString(),
            Uuid.CreateVersion4(), BulkItemDecisionProof.Schema, [.. complete.Skip(1)]);
        var swapped = await content.WriteJsonAsync<IReadOnlyList<BulkItemDecisionProof>>(fixture.TenantId.ToString(),
            Uuid.CreateVersion4(), BulkItemDecisionProof.Schema, [complete[0] with { Decision = complete[1].Decision },
                .. complete.Skip(1)]);

        // Act
        var incomplete = await fixture.ReceiveAsync(publish with { CommandId = Uuid.CreateVersion4(), ItemDecisionProofs = partial });
        var changed = await fixture.ReceiveAsync(publish with { CommandId = Uuid.CreateVersion4(), ItemDecisionProofs = swapped });
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Assert
        Assert.False(incomplete.IsSuccess);
        Assert.False(incomplete.Error!.IsTransient);
        Assert.Contains("complete selection", incomplete.Error.Message, StringComparison.Ordinal);
        Assert.False(changed.IsSuccess);
        Assert.False(changed.Error!.IsTransient);
        Assert.Contains("differs from its applied decision", changed.Error.Message, StringComparison.Ordinal);
        Assert.Null(batch.Published);
    }

    [Fact]
    public async Task ShouldWaitForFenceGivenCancellationCompletionBeforeEveryItemIsFenced()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        var abort = await fixture.BulkStepAsync(record with { State = ProcessState.Aborting }, "abort",
            nameof(CancelAccessReviewDecisionBatch));
        var fenceBefore = await FenceStepAsync(fixture, campaignId, request.DecisionBatchId, selected[0]);

        // Act
        var early = await fixture.ReceiveAsync(fenceBefore);
        await fixture.ReceiveAsync(abort);
        var onlyOne = await fixture.ReceiveAsync(fenceBefore);
        var reactorFailure = await Record.ExceptionAsync(() => RunCompletionReactorAsync(fixture, campaignId,
            request.DecisionBatchId));
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Assert
        Assert.True(early.Error!.IsTransient);
        Assert.IsType<ReceiverOutcome.Applied>(onlyOne.Value);
        Assert.IsType<ReactionCommandFailedException>(reactorFailure);
        Assert.NotNull(batch.CancellationAccepted);
        Assert.Null(batch.Canceled);
    }

    [Fact]
    public async Task ShouldResumeWithIdenticalCommandsGivenCrashDuringBatch()
    {
        // Arrange
        var crash = new CrashAfterProcessReplaces();
        await using var fixture = await AccessReviewFixture.CreateAsync(processStore: crash.Wrap);
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        crash.ArmAfterReplaceCount(5);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.RunBulkAsync(campaignId, request.DecisionBatchId));
        var held = new List<Uuid>();
        foreach (var itemId in selected)
            held.AddRange((await UseFactsAsync(fixture, campaignId, itemId)).OfType<AccessReviewItemDecisionAuthorized>()
                .Select(static fact => fact.Metadata!.EventId));
        var midway = await fixture.CampaignAsync(campaignId);
        crash.Disarm();
        var resumed = await fixture.RunBulkAsync(campaignId, request.DecisionBatchId);
        var after = new List<Uuid>();
        foreach (var itemId in selected)
            after.AddRange((await UseFactsAsync(fixture, campaignId, itemId)).OfType<AccessReviewItemDecisionAuthorized>()
                .Select(static fact => fact.Metadata!.EventId));
        var batch = await fixture.BatchAsync(campaignId, request.DecisionBatchId);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.Equal(2, held.Count);
        Assert.All(midway.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(selected.Count, after.Count);
        Assert.All(held, eventId => Assert.Contains(eventId, after));
        Assert.Single(batch.Facts.OfType<AccessReviewDecisionBatchAccepted>());
        Assert.NotNull(batch.Published);
        foreach (var itemId in selected)
            Assert.Single((await fixture.ItemHistoryAsync(campaignId, itemId)).BusinessFacts.OfType<AccessDecisionsRecorded>());
    }

    [Fact]
    public async Task ShouldStartProcessFromCommittedAdmissionGivenRootReactor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var aggregate = await fixture.CampaignAggregateAsync(campaignId);
        var record = await ReadAsync<AccessReviewDecisionBatchAdmitted>(fixture, aggregate.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reactor = new AccessReviewBulkAdmissionReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<AccessReviewManifests>(),
            scope.ServiceProvider.GetRequiredService<ISourceFactResolver>());
        await fixture.BindReactorAsync(reactor);

        // Act
        await reactor.HandleAsync(AdministratorTransitionHost
            .ReactionOf<AccessReviewDecisionBatchAdmitted>(record), CancellationToken.None);
        var process = await fixture.ProcessAsync(await fixture.BulkKey(request.DecisionBatchId));

        // Assert
        Assert.Equal(ProcessState.Completed, process!.State);
        Assert.Equal(selected.Count, process.Targets.Count);
        Assert.Equal(process.Targets.Order(StreamKeyOrder.Instance), process.Targets);
        Assert.NotNull((await fixture.BatchAsync(campaignId, request.DecisionBatchId)).Published);
    }

    [Fact]
    public async Task ShouldMatchDesignCatalogGivenBulkDefinition()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "product", "audits",
            "2026-10-23", "target-reactor-contracts.json");
        using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var entry = document.RootElement.GetProperty("processes").EnumerateArray()
            .Single(process => process.GetProperty("process").GetString() == AccessReviewEffects.BulkProcessName);

        // Act
        var definition = fixture.Provider.GetRequiredService<ProcessCatalog>().Get(AccessReviewEffects.BulkProcessName);

        // Assert
        Assert.Equal(entry.GetProperty("ordered").GetBoolean(), definition.Ordered);
        Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(e => e.GetString()),
            definition.RootSourceEvents);
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("stage").GetString()),
            definition.Stages.Select(s => s.Stage));
        Assert.Equal(entry.GetProperty("stages").EnumerateArray()
                .SelectMany(s => s.GetProperty("receivers").EnumerateArray().Select(r => r.GetString())),
            definition.Stages.SelectMany(s => s.Receivers));
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("rule").GetString()),
            definition.Stages.Select(s => s.Rule));
        Assert.Equal(entry.GetProperty("abort").EnumerateArray().Select(a => a.GetString()), definition.Abort);
    }

    [Fact]
    public async Task ShouldRefuseCancellationGivenFailureProofFromAnotherBatch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (first, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var (second, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var firstRecord = await fixture.StartBulkAsync(campaignId, first.DecisionBatchId);
        var secondRecord = await fixture.StartBulkAsync(campaignId, second.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, firstRecord, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        await ReceiveAppliedAsync(fixture, secondRecord, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        var firstCancel = (CancelAccessReviewDecisionBatch)await fixture.BulkStepAsync(firstRecord,
            "abort", nameof(CancelAccessReviewDecisionBatch));
        var secondCancel = (CancelAccessReviewDecisionBatch)await fixture.BulkStepAsync(secondRecord,
            "abort", nameof(CancelAccessReviewDecisionBatch));

        // Act
        var result = await fixture.ReceiveAsync(secondCancel with { FailureProof = firstCancel.FailureProof });
        var firstBatch = await fixture.BatchAsync(campaignId, first.DecisionBatchId);
        var secondBatch = await fixture.BatchAsync(campaignId, second.DecisionBatchId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Null(firstBatch.CancellationAccepted);
        Assert.Null(secondBatch.CancellationAccepted);
    }

    [Fact]
    public async Task ShouldRefuseCancellationGivenFailureNamesItemOutsideFrozenSelection()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
        await ReceiveAppliedAsync(fixture, record, BulkAccessDecisionProcess.Materialize,
            nameof(MaterializeAccessReviewDecisionBatch), null);
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var failure = await content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            AccessReviewContent.BulkFailureProofSchema, new AccessReviewBulkFailure("Other item failed.",
                Uuid.CreateVersion4(), record.Source));
        var cancel = (CancelAccessReviewDecisionBatch)await fixture.BulkStepAsync(record,
            "abort", nameof(CancelAccessReviewDecisionBatch));

        // Act
        var result = await fixture.ReceiveAsync(cancel with { FailureProof = failure });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Null((await fixture.BatchAsync(campaignId, request.DecisionBatchId)).CancellationAccepted);
    }

    /// <summary>Launches a campaign with the standard population and returns three bulk-eligible items.</summary>
    internal static async Task<(Uuid CampaignId, IReadOnlyList<Uuid> Selected)> LaunchedAsync(AccessReviewFixture fixture)
    {
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        return (launched.CampaignId,
        [
            AccessReviewCampaignTests.ItemId(campaign, "bot", "deploy"),
            AccessReviewCampaignTests.ItemId(campaign, "legacy", "read"),
            AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy"),
        ]);
    }

    internal static Uuid ItemOf(string target) => Uuid.Parse(target.Split('|')[1],
        System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Dispatches one process step as the trusted worker and expects it to append its fact.</summary>
    internal static async Task ReceiveAppliedAsync(AccessReviewFixture fixture, ProcessRecord record, string stage,
        string receiver, string? target)
    {
        var step = await fixture.BulkStepAsync(record, stage, receiver, target);
        var result = await fixture.ReceiveAsync(step);
        Assert.True(result.IsSuccess, $"{receiver}: {result.Error?.Message}");
        Assert.IsType<ReceiverOutcome.Applied>(result.Value);
    }

    internal static async Task<IRequest<ReceiverOutcome>> FenceStepAsync(AccessReviewFixture fixture, Uuid campaignId,
        Uuid batchId, Uuid itemId)
    {
        var campaign = await fixture.CampaignAggregateAsync(campaignId);
        var admitted = (await fixture.CampaignHistoryAsync(campaign)).AdmittedFor(batchId)!;
        var target = AccessReviewCompletionTarget.Of(fixture.TenantId, campaignId, itemId).ToString();
        return new FenceAccessReviewItemDecision(BulkAccessDecisionProcess.ReceiverCommandId(admitted.Metadata!.EventId,
                nameof(FenceAccessReviewItemDecision), target, "initial"), NativeFactReference.Of(campaign.Stream, admitted),
            AccessReviewEffects.ItemStream(fixture.TenantId, campaignId, itemId), batchId, AccessReviewEffects.Generation,
            null, admitted.Intent, campaignId, itemId, batchId);
    }

    /// <summary>Runs the cancellation reactors on the batch's cancellation facts, as the worker would.</summary>
    internal static async Task RunCancellationReactorsAsync(AccessReviewFixture fixture, Uuid campaignId, Uuid batchId)
    {
        var batch = await fixture.BatchAsync(campaignId, batchId);
        var accepted = await ReadAsync<AccessReviewDecisionBatchCancellationAccepted>(fixture, batch.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await (await fixture.BindReactorAsync(new AccessReviewBulkCancellationReactor(new InMemoryProjectionCheckpointStore(),
            services.GetRequiredService<IRequestBus>(), services.GetRequiredService<IImmutableContentStore>(),
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<AccessReviewManifests>(),
            services.GetRequiredService<ISourceFactResolver>())))
            .HandleAsync(AdministratorTransitionHost
                .ReactionOf<AccessReviewDecisionBatchCancellationAccepted>(accepted), CancellationToken.None);
        await RunCompletionReactorAsync(fixture, campaignId, batchId);
        var canceled = await ReadAsync<AccessReviewDecisionBatchCanceled>(fixture, batch.Stream);
        await (await fixture.BindReactorAsync(new AccessReviewBulkSettlementReactor(new InMemoryProjectionCheckpointStore(),
            services.GetRequiredService<IRequestBus>(), services.GetRequiredService<IImmutableContentStore>(),
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<AccessReviewManifests>(),
            services.GetRequiredService<ISourceFactResolver>())))
            .HandleAsync(AdministratorTransitionHost
                .ReactionOf<AccessReviewDecisionBatchCanceled>(canceled), CancellationToken.None);
    }

    internal static async Task RunCompletionReactorAsync(AccessReviewFixture fixture, Uuid campaignId, Uuid batchId)
    {
        var batch = await fixture.BatchAsync(campaignId, batchId);
        var accepted = await ReadAsync<AccessReviewDecisionBatchCancellationAccepted>(fixture, batch.Stream);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await (await fixture.BindReactorAsync(new AccessReviewBulkCancellationCompletionReactor(new InMemoryProjectionCheckpointStore(),
            services.GetRequiredService<IRequestBus>(), services.GetRequiredService<IImmutableContentStore>(),
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<AccessReviewManifests>(),
            services.GetRequiredService<ISourceFactResolver>())))
            .HandleAsync(AdministratorTransitionHost
                .ReactionOf<AccessReviewDecisionBatchCancellationAccepted>(accepted), CancellationToken.None);
    }

    internal static async Task<DomainEventRecord> ReadAsync<TEvent>(AccessReviewFixture fixture, EventStreamAddress stream)
        where TEvent : DomainEvent
    {
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await foreach (var record in events.ReadAsync(stream, 0))
            if (record.Event is TEvent)
                return record;
        throw new InvalidOperationException($"No {typeof(TEvent).Name} was found.");
    }

    /// <summary>The item's bulk decision use facts (holds and settlements), which are not business facts of the item.</summary>
    internal static async Task<List<DomainEvent>> UseFactsAsync(AccessReviewFixture fixture, Uuid campaignId, Uuid itemId)
    {
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var found = new List<DomainEvent>();
        try
        {
            await foreach (var record in events.ReadAsync(AccessReviewEffects.ItemStream(fixture.TenantId, campaignId,
                               itemId), 0))
                if (record.Event is AccessReviewItemDecisionAuthorized or AccessReviewItemDecisionSettled)
                    found.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unwritten stream this way.
        }

        return found;
    }
}
