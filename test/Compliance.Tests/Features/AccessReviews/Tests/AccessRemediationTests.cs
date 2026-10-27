using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
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
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessRemediationTests
{
    [Fact]
    public async Task ShouldBlockCompletionUntilLaterPopulationVerifiesGivenRevokeDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var raeAdmin = AccessReviewCampaignTests.ItemId(campaign, "rae", "admin");
        foreach (var item in campaign.Items.Where(item => item.Item.ItemId != raeAdmin))
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, item.Item.ItemId, 1,
                "keep", "Still needed."));

        // Act
        var blocked = await fixture.FailAsync(fixture.ManagerUserId, fixture.Completion(launched.CampaignId, 1),
            RequestErrorKind.Conflict);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, raeAdmin, 1, "revoke",
            "Rae changed teams."));
        await fixture.SendAsync(fixture.ApproverUserId, fixture.Change(launched.CampaignId, raeAdmin, 2, "JIRA-42",
            "Removed AdministratorAccess in the console."));
        var stillBlocked = await fixture.FailAsync(fixture.ManagerUserId, fixture.Completion(launched.CampaignId, 1),
            RequestErrorKind.Conflict);
        var (unchangedId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts(),
            DateTimeOffset.UtcNow);
        var unchanged = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Verification(launched.CampaignId, raeAdmin, 3, unchangedId), RequestErrorKind.Conflict);
        var (laterId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts(raeHasAdmin: false),
            DateTimeOffset.UtcNow);
        var verified = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Verification(launched.CampaignId, raeAdmin, 3, laterId));
        var (acknowledgment, process) = await fixture.CompleteAsync(fixture.ManagerUserId, launched.CampaignId, 1,
            attestation: "All decisions recorded and verified.");
        var completed = await fixture.CampaignAsync(launched.CampaignId);
        var final = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, completed.Completion!.SnapshotId));

        // Assert
        Assert.Contains("unresolved", blocked.Message, StringComparison.Ordinal);
        Assert.Contains("remediation", stillBlocked.Message, StringComparison.Ordinal);
        Assert.Contains("unchanged", unchanged.Message, StringComparison.Ordinal);
        Assert.Equal("removed", verified.Outcome);
        Assert.Equal(laterId, verified.PopulationId);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(process.Key.RootOperationId, acknowledgment.OperationId);
        Assert.Equal("completed", completed.Status);
        Assert.Equal(3, completed.Revision);
        var rae = Assert.Single(completed.Items, item => item.Item.ItemId == raeAdmin);
        Assert.Equal("verified", rae.RemediationStatus);
        Assert.Single(rae.ProviderChanges);
        Assert.True(final.HasIntactContent);
        Assert.Equal(AccessReviewCampaignContent.ResultKind, final.Kind);
        Assert.Equal(completed.Items.Count + 1, final.RowCount);
        Assert.Equal("All decisions recorded and verified.", completed.Completion.Attestation);
        var late = await fixture.FailAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, raeAdmin,
            rae.Revision, "keep", "Late."), RequestErrorKind.Conflict);
        Assert.Contains("completed campaign cannot change", late.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldCompleteWithApprovedExceptionGivenUnverifiedModifyDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var botDeploy = AccessReviewCampaignTests.ItemId(campaign, "bot", "deploy");
        foreach (var item in campaign.Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId, item.Item.ItemId, 1,
                item.Item.ItemId == botDeploy ? "modify" : "keep", "Reviewed."));

        // Act
        var reviewerException = await fixture.FailAsync(fixture.ManagerUserId,
            fixture.Exception(launched.CampaignId, botDeploy, 2, "Vendor fix pending."), RequestErrorKind.Forbidden);
        var exception = await fixture.SendAsync(fixture.ApproverUserId, fixture.Exception(launched.CampaignId,
            botDeploy, 2, "Vendor fix pending.", DateTimeOffset.UtcNow.AddDays(30)));
        var (_, process) = await fixture.CompleteAsync(fixture.ManagerUserId, launched.CampaignId,
            attestation: "Completed with one approved exception.");
        var completed = await fixture.CampaignAsync(launched.CampaignId);

        // Assert
        Assert.NotNull(reviewerException);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal("excepted", Assert.Single(completed.Items, item => item.Item.ItemId == botDeploy)
            .RemediationStatus);
        Assert.Equal(exception.ExceptionId, completed.Items.Single(item => item.Item.ItemId == botDeploy)
            .Exception!.ExceptionId);
        Assert.NotEqual(Uuid.Empty, completed.Completion!.SnapshotId);
    }
}
