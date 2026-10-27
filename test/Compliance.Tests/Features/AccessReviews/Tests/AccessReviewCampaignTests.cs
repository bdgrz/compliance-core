using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessReviewCampaignTests
{
    [Fact]
    public async Task ShouldHideRestrictedPopulationGivenCampaignLaunchWithoutReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(
            applicationRestricted: true);
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());

        // Act
        var denied = await fixture.FailAsync(fixture.ManagerUserId, fixture.LaunchRequest(populationId),
            RequestErrorKind.NotFound);

        // Assert
        Assert.NotNull(denied);
    }

    [Fact]
    public async Task ShouldFreezePopulationReviewersInstructionsAndDeadlineGivenLaunch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);

        // Act
        var launched = await fixture.LaunchAsync(populationId);
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Unclassified, revision: 1);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var snapshot = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, launched.SnapshotId));

        // Assert
        Assert.Equal(5, launched.ItemCount);
        Assert.Equal(launched.ItemCount, campaign.UnresolvedCount);
        Assert.Equal(1, campaign.Revision);
        Assert.All(campaign.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.All(campaign.Items, item => Assert.Equal(1, item.Revision));
        Assert.Equal("human", Assert.Single(campaign.Items, item =>
            item.Item.ProviderSubjectId == "ada" && item.Item.ProviderEntitlementId == "deploy").Item.Classification);
        var reviewer = Assert.Single(campaign.Reviewers);
        Assert.False(reviewer.Delegated);
        Assert.True(snapshot.HasIntactContent);
        Assert.Equal(AccessReviewCampaignContent.LaunchKind, snapshot.Kind);
        Assert.Equal(launched.ContentSha256, campaign.ContentSha256);
        Assert.Contains(snapshot.Rows, row => row.Key == "header");
        Assert.Contains(fixture.ProgramId.ToString(),
            snapshot.Rows.Single(row => row.Key == "header").Content.GetRawText(),
            StringComparison.Ordinal);
        Assert.Contains(fixture.RemediationOwnerMemberId.ToString(),
            snapshot.Rows.Single(row => row.Key == "header").Content.GetRawText(),
            StringComparison.Ordinal);
        var aggregate = await fixture.CampaignAggregateAsync(launched.CampaignId);
        Assert.Equal(fixture.ProgramId, aggregate.Launched!.ProgramId);
        Assert.Equal(fixture.RemediationOwnerMemberId, aggregate.Launched.RemediationOwnerMemberId);
    }

    [Fact]
    public async Task ShouldRequireCurrentProgramManagementGivenIdempotentLaunchReplay()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var request = fixture.LaunchRequest(populationId);
        await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess();
        fixture.Permissions.Deny(fixture.ManagerUserId, RbacPermissions.ProgramManage);

        // Act
        var refused = await fixture.As(fixture.ManagerUserId).When(request).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Contains("currently manage", refused.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHideRestrictedCampaignGivenIdempotentLaunchReplayWithoutSystemReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(applicationRestricted: true);
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.AllowManagerRestrictedRead();
        fixture.Permissions.AllowRestrictedRead(fixture.ReviewerUserId);
        await fixture.ClassifyStandardAsync(populationId);
        var request = fixture.LaunchRequest(populationId);
        await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess();
        fixture.DenyManagerRestrictedRead();

        // Act
        var replay = await fixture.As(fixture.ManagerUserId).When(request)
            .ExpectFailure(RequestErrorKind.NotFound);
        var changedReplay = await fixture.As(fixture.ManagerUserId).When(request with { Name = "Changed campaign" })
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Contains("not found", replay.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", changedReplay.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldHideRestrictedLegacyCampaignGivenIdempotentLaunchReplayWithoutSystemReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(applicationRestricted: true);
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.AllowManagerRestrictedRead();
        fixture.Permissions.AllowRestrictedRead(fixture.ReviewerUserId);
        await fixture.ClassifyStandardAsync(populationId);
        var request = fixture.LaunchRequest(populationId);
        var launched = (await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess()).Value;
        await fixture.MaterializeAsync(launched.CampaignId);
        var (legacyCampaignId, _) = await fixture.SeedLegacyCampaignAsync(launched.CampaignId);
        fixture.DenyManagerRestrictedRead();
        var legacyRequest = request with { CampaignId = legacyCampaignId };

        // Act
        var replay = await fixture.As(fixture.ManagerUserId).When(legacyRequest)
            .ExpectFailure(RequestErrorKind.NotFound);
        var changedReplay = await fixture.As(fixture.ManagerUserId).When(legacyRequest with { Name = "Changed campaign" })
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Contains("not found", replay.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", changedReplay.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectChangedFrozenContentGivenIdempotentLaunchReplay()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        fixture.Sources.AccessOwner = fixture.ApproverMemberId;
        var assignment = new AccessReviewAssignment(populationId, fixture.ReviewerMemberId,
            "Assigned as the trained access-review delegate.");
        var request = new LaunchAccessReviewCampaign(Uuid.CreateVersion4(), fixture.TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [assignment], fixture.ProgramId, fixture.RemediationOwnerMemberId);
        var launched = (await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess()).Value;
        var replay = (await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess()).Value;
        var changedRequests = new[]
        {
            request with { Name = "Changed name" },
            request with { Instructions = "Changed instructions" },
            request with { Deadline = request.Deadline.AddDays(1) },
            request with
            {
                Assignments = [assignment with { ReviewerMemberId = fixture.ApproverMemberId }],
            },
            request with
            {
                Assignments = [assignment with { DelegationReason = "Changed delegation basis." }],
            },
            request with { Assignments = [assignment with { PopulationId = Uuid.CreateVersion4() }] },
            request with { ProgramId = Uuid.CreateVersion4() },
            request with { RemediationOwnerMemberId = Uuid.CreateVersion4() },
        };

        // Act
        var conflicts = new List<RequestError>();
        foreach (var changedRequest in changedRequests)
            conflicts.Add((await fixture.As(fixture.ManagerUserId).When(changedRequest)
                .ExpectFailure(RequestErrorKind.Conflict)).Error!);
        var aggregate = await fixture.CampaignAggregateAsync(launched.CampaignId);

        // Assert
        Assert.Equal(launched, replay);
        Assert.Equal(changedRequests.Length, conflicts.Count);
        Assert.All(conflicts, conflict => Assert.Contains("different", conflict.Message,
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, aggregate.Revision);
        Assert.Single((await fixture.CampaignHistoryAsync(aggregate)).Facts);
    }

    [Fact]
    public async Task ShouldRequireDelegationReasonGivenReviewerOtherThanAccessOwner()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var launch = new LaunchAccessReviewCampaign(Uuid.CreateVersion4(), fixture.TenantId, "Review", "Instructions",
            DateTimeOffset.UtcNow.AddDays(7),
            [new AccessReviewAssignment(populationId, fixture.ApproverMemberId)],
            fixture.ProgramId, fixture.RemediationOwnerMemberId);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, launch, RequestErrorKind.Validation);
        var delegated = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Access owner on leave.");
        var campaign = await fixture.CampaignAsync(delegated.CampaignId);

        // Assert
        Assert.Contains("delegat", refused.Message, StringComparison.Ordinal);
        Assert.True(Assert.Single(campaign.Reviewers).Delegated);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldRejectCampaignLaunchGivenMissingOwnerProgramOrRemediationOwner(
        bool includeProgram, bool includeRemediationOwner)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var launch = new LaunchAccessReviewCampaign(Uuid.CreateVersion4(), fixture.TenantId, "Q3 review",
            "Review access.", DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
            includeProgram ? fixture.ProgramId : null,
            includeRemediationOwner ? fixture.RemediationOwnerMemberId : null);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, launch, RequestErrorKind.Validation);

        // Assert
        Assert.Contains("owner Program", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectCampaignLaunchGivenReviewerLacksCurrentQueueRead()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.Permissions.Deny(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, fixture.LaunchRequest(populationId),
            RequestErrorKind.Validation);

        // Assert
        Assert.Contains("program queue", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectCampaignLaunchGivenRemediationOwnerLacksAccessReviewPermission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.Services.RbacPermissions.AccessReviewManage);

        // Act
        var refused = await fixture.FailAsync(fixture.ApproverUserId, fixture.LaunchRequest(populationId),
            RequestErrorKind.Validation);

        // Assert
        Assert.Contains("remediation owner", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRecordAttributableDecisionsAndLimitParticipantsGivenAssignedReviewer()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var adaDeploy = ItemId(campaign, "ada", "deploy");
        var raeAdmin = ItemId(campaign, "rae", "admin");

        // Act
        var decision = await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(
            launched.CampaignId, adaDeploy, 1, "revoke", "No longer deploys."));
        var selfReview = await fixture.FailAsync(fixture.ReviewerUserId, fixture.Decision(
            launched.CampaignId, raeAdmin, 1, "keep", "Mine."), RequestErrorKind.Forbidden);
        var outsiderDecision = await fixture.FailAsync(fixture.OutsiderUserId, fixture.Decision(
            launched.CampaignId, adaDeploy, 2, "keep", "Keep."), RequestErrorKind.NotFound);
        var outsiderRead = await fixture.FailAsync(fixture.OutsiderUserId,
            new GetAccessReviewCampaign(fixture.TenantId, launched.CampaignId), RequestErrorKind.NotFound);
        var reviewerView = await fixture.CampaignAsync(launched.CampaignId, fixture.ReviewerUserId);

        // Assert
        Assert.Equal(fixture.ReviewerMemberId, decision.ReviewerMemberId);
        Assert.Equal("revoke", decision.Decision);
        Assert.False(decision.AfterDeadline);
        Assert.Contains("own access", selfReview.Message, StringComparison.Ordinal);
        Assert.NotNull(outsiderDecision);
        Assert.NotNull(outsiderRead);
        var decided = Assert.Single(reviewerView.Items, item => item.Item.ItemId == adaDeploy);
        Assert.Equal("decided", decided.Status);
        Assert.Equal(2, decided.Revision);
        Assert.Equal(1, reviewerView.Revision);
        Assert.Equal(campaign.Items.Count - 1, reviewerView.UnresolvedCount);
    }

    [Fact]
    public async Task ShouldReassignFrozenReviewerOnlyAfterEligibilityLossGivenRoutedCampaign()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        fixture.Sources.AccessOwner = fixture.ApproverMemberId;
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var request = new ReassignAccessReviewResponsibility(Uuid.CreateVersion4(), fixture.TenantId,
            launched.CampaignId, itemId, AccessReviewResponsibilityKind.Reviewer, 1, fixture.ReviewerMemberId,
            "Original reviewer lost current queue access.",
            "Appointed a trained member as a delegate for this system.");

        // Act
        var eligibleOwner = await fixture.FailAsync(fixture.ManagerUserId, request,
            RequestErrorKind.Conflict);
        fixture.Permissions.Deny(fixture.ApproverUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);
        var missingDelegation = await fixture.FailAsync(fixture.ManagerUserId,
            request with { DelegationReason = null }, RequestErrorKind.Validation);
        var reassignment = (await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess()).Value;
        var replay = (await fixture.As(fixture.ManagerUserId).When(request).ExpectSuccess()).Value;
        var conflictingRevisionReplay = await fixture.As(fixture.ManagerUserId)
            .When(request with { ExpectedRevision = request.ExpectedRevision + 1 })
            .ExpectFailure(RequestErrorKind.Conflict);
        var conflictingReplay = await fixture.As(fixture.ManagerUserId)
            .When(request with { DelegationReason = "Different delegate basis." })
            .ExpectFailure(RequestErrorKind.Conflict);
        var reassigned = await fixture.CampaignAsync(launched.CampaignId);
        var oldReviewerDecision = await fixture.FailAsync(fixture.ApproverUserId,
            fixture.Decision(launched.CampaignId, itemId, 2, "keep", "Still required."), RequestErrorKind.NotFound);
        var staleRevision = await fixture.FailAsync(fixture.ManagerUserId, request with
        {
            CommandId = Uuid.CreateVersion4(),
            AssignedMemberId = fixture.ApproverMemberId,
            Reason = "Revision changed.",
        }, RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("loses eligibility", eligibleOwner.Message, StringComparison.Ordinal);
        Assert.Contains("delegate", missingDelegation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fixture.ApproverMemberId, reassignment.PreviousMemberId);
        Assert.Equal(fixture.ReviewerMemberId, reassignment.AssignedMemberId);
        Assert.Equal(reassignment, replay);
        Assert.Equal(RequestErrorKind.Conflict, conflictingRevisionReplay.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, conflictingReplay.Error!.Kind);
        var state = Assert.Single(reassigned.Items, item => item.Item.ItemId == itemId);
        Assert.Equal(fixture.ApproverMemberId, state.Item.ReviewerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, state.CurrentReviewerMemberId);
        Assert.Equal(reassignment, Assert.Single(state.ResponsibilityReassignments!));
        Assert.Equal(2, state.Revision);
        Assert.Equal("Appointed a trained member as a delegate for this system.",
            reassignment.DelegationReason);
        Assert.NotNull(oldReviewerDecision);
        Assert.Contains("revision", staleRevision.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRequireCurrentRemediationOwnerAfterAuditedReassignmentGivenLostEligibility()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Remediation test reviewer.");
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await fixture.SendAsync(fixture.ApproverUserId, fixture.Decision(launched.CampaignId, itemId, 1, "revoke",
            "No longer required."));
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.Services.RbacPermissions.AccessReviewManage);
        var reassign = new ReassignAccessReviewResponsibility(Uuid.CreateVersion4(), fixture.TenantId,
            launched.CampaignId, itemId, AccessReviewResponsibilityKind.RemediationOwner, 2,
            fixture.ReviewerMemberId, "Original owner lost access-review authority.");

        // Act
        var assignment = await fixture.SendAsync(fixture.ApproverUserId, reassign);
        var oldOwnerChange = await fixture.FailAsync(fixture.ApproverUserId,
            fixture.Change(launched.CampaignId, itemId, 3), RequestErrorKind.NotFound);
        var change = await fixture.SendAsync(fixture.ReviewerUserId, fixture.Change(launched.CampaignId, itemId, 3));
        var updated = await fixture.CampaignAsync(launched.CampaignId, fixture.ApproverUserId);

        // Assert
        Assert.Equal(fixture.ManagerMemberId, assignment.PreviousMemberId);
        Assert.Equal(fixture.ReviewerMemberId, assignment.AssignedMemberId);
        Assert.NotNull(oldOwnerChange);
        var item = Assert.Single(updated.Items, state => state.Item.ItemId == itemId);
        Assert.Equal(fixture.ManagerMemberId, updated.RemediationOwnerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, item.CurrentRemediationOwnerMemberId);
        Assert.Equal(fixture.ReviewerMemberId.ToString(), change.RecordedBy.Id);
    }

    [Fact]
    public async Task ShouldKeepLegacyCampaignTenantDirectGivenNoProgramQueueReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        var (campaignId, itemId) = await fixture.SeedLegacyCampaignAsync(routed.CampaignId);
        fixture.Permissions.Deny(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);

        // Act
        var visible = await fixture.CampaignAsync(campaignId, fixture.ReviewerUserId);
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            fixture.Decision(campaignId, itemId, 1, "revoke", "No longer required."));
        var change = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Change(campaignId, itemId, 2, "ticket-legacy", "Disable the old grant."));

        // Assert
        Assert.Equal(fixture.ReviewerMemberId, Assert.Single(visible.Items,
            item => item.Item.ItemId == itemId).CurrentReviewerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, decision.ReviewerMemberId);
        Assert.Equal(fixture.ManagerMemberId.ToString(), change.RecordedBy.Id);
    }

    [Fact]
    public async Task ShouldBindPreviewToItemHeadsAndExcludePrivilegedGivenBulkDecisionPreview()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.PrivilegedEntitlement,
            new AccessExpectationParameters(EntitlementKind: "admin_privilege"));
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var adaAdmin = ItemId(campaign, "ada", "admin");
        var botDeploy = ItemId(campaign, "bot", "deploy");
        var legacyRead = ItemId(campaign, "legacy", "read");
        var request = new PreviewBulkAccessDecision(fixture.TenantId, launched.CampaignId,
            [adaAdmin, botDeploy, legacyRead], "keep");

        // Act
        var preview = await fixture.SendAsync(fixture.ReviewerUserId, request);
        var again = await fixture.SendAsync(fixture.ReviewerUserId, request);
        await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(launched.CampaignId, botDeploy, 1, "keep",
            "Needed."));
        var afterItemChange = await fixture.SendAsync(fixture.ReviewerUserId, request);

        // Assert
        Assert.Equal("privileged_requires_individual_decision", Assert.Single(preview.Rejected).Reason);
        Assert.Equal(2, preview.Eligible.Count);
        Assert.Equal(preview.PreviewToken, again.PreviewToken);
        Assert.NotEqual(preview.PreviewToken, afterItemChange.PreviewToken);
        Assert.Equal(1, afterItemChange.Revision);
    }

    internal static Uuid ItemId(AccessReviewCampaignView campaign, string subject, string entitlement) =>
        Assert.Single(campaign.Items, item => item.Item.ProviderSubjectId == subject &&
                                              item.Item.ProviderEntitlementId == entitlement).Item.ItemId;
}
