using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class FitzAccessReviewCampaignImpactDirectoryTests
{
    [Fact]
    public async Task ShouldIndexHistoricalCampaignSystemsAndReadCurrentCompletionGivenReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var firstSystemInstanceId = Uuid.CreateVersion4();
        var secondSystemInstanceId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var finalSnapshotId = Uuid.CreateVersion4();
        var launchedAt = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var completedAt = launchedAt.AddDays(1);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Review owner");
        var launched = new AccessReviewCampaignLaunched(tenantId, campaignId,
            "Quarterly access review", "Review access", launchedAt.AddDays(14), snapshotId,
            new string('a', 64), [],
            [Item(firstSystemInstanceId), Item(firstSystemInstanceId),
             Item(secondSystemInstanceId)], actor, launchedAt);
        var completed = new AccessReviewCampaignCompleted(tenantId, campaignId, 2,
            new AccessReviewCampaignCompletionView(finalSnapshotId, new string('b', 64),
                "No exceptions remain.", actor, completedAt));
        var directory = new FitzAccessReviewCampaignImpactDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(AccessReviewCampaignImpactSchema.Projector,
            AccessReviewCampaignImpactSchema.Pattern(tenantId));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(launched);
            await directory.ApplyAsync(completed);
            await directory.ApplyAsync(launched);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var firstSystemReferences = await directory.ListBySystemInstanceAsync(tenantId,
            firstSystemInstanceId, 10, null);
        var secondSystemReferences = await directory.ListBySystemInstanceAsync(tenantId,
            secondSystemInstanceId, 10, null);
        var summary = await directory.GetCampaignAsync(tenantId, campaignId);
        var otherTenantReferences = await directory.ListBySystemInstanceAsync(Uuid.CreateVersion4(),
            firstSystemInstanceId, 10, null);

        // Assert
        var firstReference = Assert.Single(firstSystemReferences.Items);
        var secondReference = Assert.Single(secondSystemReferences.Items);
        Assert.Equal(campaignId, firstReference.CampaignId);
        Assert.Equal(campaignId, secondReference.CampaignId);
        Assert.Equal(launchedAt, firstReference.LaunchedAt);
        Assert.Equal(AccessReviewCampaign.Completed, summary?.Status);
        Assert.Equal(finalSnapshotId, summary?.FinalSnapshotId);
        Assert.Equal(completedAt, summary?.CompletedAt);
        Assert.Empty(otherTenantReferences.Items);
    }

    static AccessReviewItemView Item(Uuid systemInstanceId) => new(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), systemInstanceId, Uuid.CreateVersion4(),
        "provider-subject", "user", "Employee", "confidential", Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), "payroll-role", "role", "Payroll role", true, "none", [], null);
}
