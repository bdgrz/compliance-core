using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     The item commands are identified 200 operations: an exact retry returns the original frozen result even after
///     the item changed again, and the same identity with any changed field conflicts.
/// </summary>
public sealed class AccessReviewIdentifiedOperationTests
{
    static string Wire<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);

    [Fact]
    public async Task ShouldReturnOriginalDecisionGivenExactRetryAfterLaterItemChange()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemId, request) = await RevokedItemAsync(fixture);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Change(campaignId, itemId, 2));

        // Act
        var retried = await fixture.SendAsync(fixture.ReviewerUserId, request);

        // Assert
        Assert.Equal("revoke", retried.Decision);
        Assert.Equal(request.CommandId, retried.DecisionId);
        Assert.Equal(3, await fixture.ItemRevisionAsync(campaignId, itemId));
        Assert.Equal(Wire(retried), Wire(await fixture.SendAsync(fixture.ReviewerUserId, request)));
    }

    [Fact]
    public async Task ShouldConflictGivenSameDecisionIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemId, request) = await RevokedItemAsync(fixture);

        // Act
        var rationale = await fixture.FailAsync(fixture.ReviewerUserId, request with { Rationale = "Different." },
            RequestErrorKind.Conflict);
        var decision = await fixture.FailAsync(fixture.ReviewerUserId, request with { Decision = "keep" },
            RequestErrorKind.Conflict);
        var revision = await fixture.FailAsync(fixture.ReviewerUserId, request with { ExpectedRevision = 2 },
            RequestErrorKind.Conflict);
        var otherActor = await fixture.FailAsync(fixture.ManagerUserId, request, RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("different", rationale.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("different", decision.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(revision);
        Assert.NotNull(otherActor);
        Assert.Equal(2, await fixture.ItemRevisionAsync(campaignId, itemId));
    }

    [Fact]
    public async Task ShouldReturnOriginalChangeAndExceptionAndVerificationGivenExactRetriesAfterLaterChanges()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemId, _) = await RevokedItemAsync(fixture);
        var change = fixture.Change(campaignId, itemId, 2, "ticket-9", "Disable the grant.");
        var firstChange = await fixture.SendAsync(fixture.ManagerUserId, change);
        var exception = fixture.Exception(campaignId, itemId, 3, "Compensating control.",
            fixture.Clock.GetUtcNow().AddDays(30));
        var firstException = await fixture.SendAsync(fixture.ApproverUserId, exception);
        var currentFacts = AccessReviewFixture.StandardFacts();
        var laterFacts = new AccessPopulationFacts(currentFacts.Principals, currentFacts.Entitlements,
            currentFacts.GroupMembers.Where(static membership => membership.GroupProviderSubjectId != "deployer" ||
                                                                 membership.MemberProviderSubjectId != "ada").ToArray(),
            currentFacts.Assignments);
        await Task.Delay(20);
        var (laterId, _) = await fixture.AcceptAsync(laterFacts, DateTimeOffset.UtcNow);
        var verification = fixture.Verification(campaignId, itemId, 4, laterId);
        var firstVerification = await fixture.SendAsync(fixture.ManagerUserId, verification);
        await fixture.SendAsync(fixture.ManagerUserId, fixture.Change(campaignId, itemId, 5, "ticket-10", "Later."));

        // Act
        var changeAgain = await fixture.SendAsync(fixture.ManagerUserId, change);
        var exceptionAgain = await fixture.SendAsync(fixture.ApproverUserId, exception);
        var verificationAgain = await fixture.SendAsync(fixture.ManagerUserId, verification);

        // Assert
        Assert.Equal(Wire(firstChange), Wire(changeAgain));
        Assert.Equal(Wire(firstException), Wire(exceptionAgain));
        Assert.Equal(Wire(firstVerification), Wire(verificationAgain));
        Assert.Equal(6, await fixture.ItemRevisionAsync(campaignId, itemId));
    }

    [Fact]
    public async Task ShouldConflictGivenSameChangeAndExceptionIdentityWithChangedIntent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemId, _) = await RevokedItemAsync(fixture);
        var change = fixture.Change(campaignId, itemId, 2, "ticket-9", "Disable the grant.");
        await fixture.SendAsync(fixture.ManagerUserId, change);
        var exception = fixture.Exception(campaignId, itemId, 3, "Compensating control.");
        await fixture.SendAsync(fixture.ApproverUserId, exception);

        // Act
        var changedChange = await fixture.FailAsync(fixture.ManagerUserId, change with { Description = "Different." },
            RequestErrorKind.Conflict);
        var changedException = await fixture.FailAsync(fixture.ApproverUserId, exception with { Rationale = "Different." },
            RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("different", changedChange.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("different", changedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, await fixture.ItemRevisionAsync(campaignId, itemId));
    }

    [Fact]
    public async Task ShouldRejectGivenMissingCommandIdentity()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, itemId, request) = await RevokedItemAsync(fixture);

        // Act
        var decision = await fixture.FailAsync(fixture.ReviewerUserId, request with { CommandId = Uuid.Empty },
            RequestErrorKind.Validation);
        var change = await fixture.FailAsync(fixture.ManagerUserId, fixture.Change(campaignId, itemId, 2) with
        {
            CommandId = Uuid.Empty,
        }, RequestErrorKind.Validation);
        var completion = await fixture.FailAsync(fixture.ManagerUserId, fixture.Completion(campaignId, 1) with
        {
            CommandId = Uuid.Empty,
        }, RequestErrorKind.Validation);

        // Assert
        Assert.Contains("operation identity", decision.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operation identity", change.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operation identity", completion.Message, StringComparison.OrdinalIgnoreCase);
    }

    static async Task<(Uuid CampaignId, Uuid ItemId, RecordAccessDecision Request)> RevokedItemAsync(
        AccessReviewFixture fixture)
    {
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy");
        var request = fixture.Decision(launched.CampaignId, itemId, 1, "revoke", "No longer deploys.");
        await fixture.SendAsync(fixture.ReviewerUserId, request);
        return (launched.CampaignId, itemId, request);
    }
}
