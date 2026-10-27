using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class MethodologyTemplateTests
{
    static readonly Uuid TemplateId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPublishIndependentlyReviewedImmutableVersionAndRetainAuthorshipGivenSuccessiveDraft()
    {
        // Arrange
        await using var template = new MethodologyTemplateScenario(Now);

        // Act
        var created = await template.CreateAsync(new CreateMethodologyTemplate(TemplateId, Content("initial"),
            "Initial version."), AuthorId);
        template.Clock.Now = Now.AddMinutes(1);
        var selfReview = await template.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), TemplateId,
            1, "approved", "Ready for independent review."), AuthorId);
        template.Clock.Now = Now.AddMinutes(2);
        var reviewed = await template.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), TemplateId,
            1, "approved", "Checked for client identifiers and license scope."), ReviewerId);
        template.Clock.Now = Now.AddMinutes(3);
        var published = await template.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            TemplateId, 1, "Approved for platform use."), ApproverId);
        var priorVersion = Assert.Single((await template.Views.ReadAsync(TemplateId))!.Versions);
        template.Clock.Now = Now.AddMinutes(4);
        var revised = await template.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(),
            TemplateId, 3, Content("successor"), "Updated onboarding control."), AuthorId);
        var view = await template.Views.ReadAsync(TemplateId);

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, selfReview.Error?.Kind);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(published.IsSuccess, published.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.Equal("published", priorVersion.Status);
        Assert.Equal(RbacIds.Member(Uuid.Empty, AuthorId), priorVersion.AuthoredByMemberId);
        Assert.Equal(RbacIds.Member(Uuid.Empty, ApproverId), priorVersion.ApprovedByMemberId);
        Assert.Equal("initial", priorVersion.Content.Records.Single().Summary);
        Assert.Equal(2, view!.Versions.Count);
        Assert.Equal("draft", view.Versions[^1].Status);
        Assert.Equal("successor", view.Versions[^1].Content.Records.Single().Summary);
        Assert.Equal("initial", view.Versions[0].Content.Records.Single().Summary);
    }

    [Fact]
    public async Task ShouldReplayVersionReviewsAndApprovalGivenRetainedTemplateEvents()
    {
        // Arrange
        await using var template = new MethodologyTemplateScenario(Now);
        Assert.True((await template.CreateAsync(new CreateMethodologyTemplate(TemplateId, Content("first"),
            "Initial version."), AuthorId)).IsSuccess);
        template.Clock.Now = Now.AddMinutes(1);
        Assert.True((await template.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), TemplateId,
            1, "approved", "Reviewed."), ReviewerId)).IsSuccess);
        template.Clock.Now = Now.AddMinutes(2);
        Assert.True((await template.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            TemplateId, 1, "Approved."), ApproverId)).IsSuccess);
        var events = await template.FactsAsync(TemplateId);
        var original = await template.Views.ReadAsync(TemplateId);

        // Act
        var replay = new AggregateScenario<MethodologyTemplate>(new MethodologyTemplate(TemplateId))
            .Given(events.ToArray()).Aggregate;
        var replayView = await template.Views.ReadAsync(replay, CancellationToken.None);

        // Assert
        Assert.Equal(original!.ContentSha256, replayView!.ContentSha256);
        Assert.Equal("published", replay.VersionStatus);
        Assert.Equal("published", replayView.Versions.Single().Status);
        Assert.Equal(RbacIds.Member(Uuid.Empty, ReviewerId), replayView.Versions.Single().Review!.ReviewedByMemberId);
    }

    internal static MethodologyTemplateContent Content(string summary) => new(
        "SOC 2 starter methodology", "Original control procedures for firm use.", "soc2",
        [new("cc6-1-access-review", "control", "Review privileged access",
            summary, [new(Uuid.CreateVersion4(), "CC6.1")])]);
}
