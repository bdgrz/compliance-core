using Bdgrz.Compliance.Features.MethodologyTemplates;
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
    public void ShouldPublishIndependentlyReviewedImmutableVersionAndRetainAuthorshipGivenSuccessiveDraft()
    {
        // Arrange
        var template = new MethodologyTemplate(TemplateId);

        // Act
        var created = template.Create(Content("initial"), "Initial version.", AuthorId, "Practice lead", Now);
        var selfReview = template.ReviewDraft(template.Revision, "approved", "Ready for independent review.",
            AuthorId, "Practice lead", Now.AddMinutes(1));
        var reviewed = template.ReviewDraft(template.Revision, "approved", "Checked for client identifiers and license scope.",
            ReviewerId, "Independent reviewer", Now.AddMinutes(2));
        var published = template.ApproveVersion(template.Revision, "Approved for platform use.",
            ApproverId, "Platform approver", Now.AddMinutes(3));
        var priorVersion = template.Current!.Versions.Single();
        var revised = template.ReviseDraft(template.Revision, Content("successor"), "Updated onboarding control.",
            AuthorId, "Practice lead", Now.AddMinutes(4));

        // Assert
        Assert.Null(created);
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Null(reviewed);
        Assert.Null(published);
        Assert.Null(revised);
        Assert.Equal("published", priorVersion.Status);
        Assert.Equal(AuthorId, priorVersion.AuthoredByMemberId);
        Assert.Equal(ApproverId, priorVersion.ApprovedByMemberId);
        Assert.Equal("initial", priorVersion.Content.Records.Single().Summary);
        Assert.Equal(2, template.Current.Versions.Count);
        Assert.Equal("draft", template.Current.Versions[^1].Status);
        Assert.Equal("successor", template.Current.Versions[^1].Content.Records.Single().Summary);
        Assert.Equal("initial", template.Current.Versions[0].Content.Records.Single().Summary);
    }

    [Fact]
    public void ShouldReplayVersionReviewsAndApprovalGivenRetainedTemplateEvents()
    {
        // Arrange
        var template = new MethodologyTemplate(TemplateId);
        Assert.Null(template.Create(Content("first"), "Initial version.", AuthorId, "Practice lead", Now));
        Assert.Null(template.ReviewDraft(template.Revision, "approved", "Reviewed.", ReviewerId,
            "Independent reviewer", Now.AddMinutes(1)));
        Assert.Null(template.ApproveVersion(template.Revision, "Approved.", ApproverId,
            "Platform approver", Now.AddMinutes(2)));
        var events = new AggregateScenario<MethodologyTemplate>(template).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<MethodologyTemplate>(new MethodologyTemplate(TemplateId))
            .Given(events).Aggregate;

        // Assert
        Assert.Equal(template.Current!.ContentSha256, replay.Current!.ContentSha256);
        Assert.Equal("published", replay.Current.Versions.Single().Status);
        Assert.Equal(ReviewerId, replay.Current.Versions.Single().Review!.ReviewedByMemberId);
    }

    internal static MethodologyTemplateContent Content(string summary) => new(
        "SOC 2 starter methodology", "Original control procedures for firm use.", "soc2",
        [new("cc6-1-access-review", "control", "Review privileged access",
            summary, [new(Uuid.CreateVersion4(), "CC6.1")])]);
}
