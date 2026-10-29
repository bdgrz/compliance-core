using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateOwnerTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("content")]
    [InlineData("content_body")]
    [InlineData("reviewer")]
    [InlineData("review_outcome")]
    [InlineData("approver")]
    [InlineData("approval_time")]
    [InlineData("rationale")]
    [InlineData("valid")]
    public async Task ShouldPublishOnlyExactReviewedVersionGivenPreparedPublication(string defect)
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = new MethodologyTemplateScenario(now);
        var templateId = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        Assert.True((await fixture.CreateAsync(new CreateMethodologyTemplate(templateId,
            MethodologyTemplateTests.Content("draft"), "Initial"), author)).IsSuccess);
        fixture.Clock.Now = now.AddMinutes(1);
        Assert.True((await fixture.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), templateId,
            1, "approved", "Reviewed"), reviewer)).IsSuccess);
        var commandId = Uuid.CreateVersion4();
        var owner = await fixture.Reader.HydrateAsync(new MethodologyTemplate(templateId, commandId));
        var scenario = new AggregateScenario<MethodologyTemplate>(owner);
        var member = RbacIds.Member(Uuid.Empty, approver);
        var approvedAt = now.AddMinutes(2);
        var version = (await fixture.Views.VersionAsync(templateId, 1))! with
        {
            Status = "published",
            ApprovedByMemberId = member,
            ApprovedByDisplay = "Approver",
            ApprovedAt = approvedAt,
            ApprovalRationale = "Approved",
        };
        version = defect switch
        {
            "version" => version with { VersionId = Uuid.CreateVersion4() },
            "content" => version with { ContentSha256 = new string('c', 64) },
            "content_body" => version with { Content = version.Content with { Name = "Changed content" } },
            "reviewer" => version with
            {
                Review = version.Review! with { ReviewedByMemberId = Uuid.CreateVersion4() },
            },
            "review_outcome" => version with { Review = version.Review! with { Outcome = "changes_requested" } },
            "approver" => version with { ApprovedByMemberId = Uuid.CreateVersion4() },
            "approval_time" => version with { ApprovedAt = approvedAt.AddMinutes(1) },
            "rationale" => version with { ApprovalRationale = null },
            "valid" => version,
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        var operation = OwnerProvenance.For(owner, commandId, nameof(ApproveMethodologyTemplateVersion),
            ActorReference.ForPlatformOperator(approver, "Approver"), templateId.ToString(), null,
            new string('a', 64), now.AddMinutes(2));
        var revision = owner.Revision;
        var versionId = owner.VersionId;
        var reviewedBy = owner.ReviewerMemberId;

        // Act
        var result = owner.Append(nameof(ApproveMethodologyTemplateVersion), revision, operation, member,
            version, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        if (defect == "valid")
        {
            Assert.True(result.IsSuccess);
            Assert.Single(scenario.PendingEvents);
            Assert.Equal("published", owner.VersionStatus);
            Assert.Equal(versionId, owner.PublishedVersionId);
            return;
        }
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(revision, owner.Revision);
        Assert.Equal("reviewed", owner.VersionStatus);
        Assert.Equal(versionId, owner.VersionId);
        Assert.Equal(reviewedBy, owner.ReviewerMemberId);
        Assert.Equal(0, owner.PublishedVersionNumber);
        Assert.Equal(Uuid.Empty, owner.PublishedVersionId);
        Assert.False(owner.IsRetired);
    }
}
