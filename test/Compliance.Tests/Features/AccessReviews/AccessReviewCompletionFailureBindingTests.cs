using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

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
