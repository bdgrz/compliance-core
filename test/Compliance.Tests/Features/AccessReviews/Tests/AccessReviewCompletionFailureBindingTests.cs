using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessReviewCompletionFailureBindingTests
{
    [Fact]
    public async Task ShouldKeepLaterCompletionAndHeldItemGivenEarlierCompletionFailureProof()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.", fixture.ApproverMemberId);
        var view = await fixture.CampaignAsync(launched.CampaignId);
        foreach (var item in view.Items)
            await fixture.SendAsync(fixture.ManagerUserId, fixture.Decision(launched.CampaignId,
                item.Item.ItemId, 1, "keep", "Still needed."));
        var first = fixture.Completion(launched.CampaignId, 1);
        await fixture.SendAsync(fixture.ManagerUserId, first);
        var firstProcess = await fixture.StartCompletionAsync(launched.CampaignId, first.CommandId);
        var firstAbort = (AbortAccessReviewCampaignCompletion)await fixture.CompletionStepAsync(firstProcess,
            "abort", nameof(AbortAccessReviewCampaignCompletion));
        var canceled = await fixture.ReceiveAsync(firstAbort);
        Assert.True(canceled.IsSuccess, canceled.Error?.Message);
        var second = fixture.Completion(launched.CampaignId, 3);
        await fixture.SendAsync(fixture.ManagerUserId, second);
        var secondProcess = await fixture.StartCompletionAsync(launched.CampaignId, second.CommandId);
        var hold = await fixture.CompletionStepAsync(secondProcess, AccessReviewCompletionProcess.Acquire,
            nameof(AuthorizeAccessReviewItemCampaignCompletionUse), secondProcess.Targets[0]);
        var held = await fixture.ReceiveAsync(hold);
        Assert.True(held.IsSuccess, held.Error?.Message);
        var secondAbort = (AbortAccessReviewCampaignCompletion)await fixture.CompletionStepAsync(secondProcess,
            "abort", nameof(AbortAccessReviewCampaignCompletion));
        var heldItemId = AccessReviewBulkDecisionTests.ItemOf(secondProcess.Targets[0]);

        // Act
        var refused = await fixture.ReceiveAsync(secondAbort with { FailureProof = firstAbort.FailureProof });
        var owner = await fixture.CampaignAggregateAsync(launched.CampaignId);
        var itemOwner = await fixture.ItemAsync(launched.CampaignId, heldItemId);

        // Assert
        Assert.False(refused.IsSuccess, $"Earlier completion failure was accepted: {refused.Error?.Message}");
        Assert.Equal(RequestErrorKind.Conflict, refused.Error!.Kind);
        Assert.False(refused.Error.IsTransient);
        Assert.Equal(second.CommandId, owner.Pending!.Operation.RootOperationId);
        Assert.True(itemOwner.Uses.HasActiveUse);
        Assert.False(itemOwner.IsClosed);
    }
}
