using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessReviewNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedItemAuthorityGivenManyValidPersonalDecisions()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var itemId = selected[0];
        for (var index = 0; index < 64; index++)
            await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, itemId,
                await fixture.ItemRevisionAsync(campaignId, itemId), "keep", $"Reviewed justification {index + 1}."));

        // Act
        var owner = await fixture.ItemAsync(campaignId, itemId);
        var retained = typeof(AccessReviewItem).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(65, owner.Revision);
        Assert.False(owner.IsClosed);
        Assert.False(owner.Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldKeepBoundedCampaignAuthorityGivenManyAdmittedIndependentBatches()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        for (var index = 0; index < 32; index++)
            await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);

        // Act
        var owner = await fixture.CampaignAggregateAsync(campaignId);
        var retained = typeof(AccessReviewCampaign).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(33, owner.Revision);
        Assert.NotNull(owner.Launched);
        Assert.Null(owner.Closed);
    }
}
