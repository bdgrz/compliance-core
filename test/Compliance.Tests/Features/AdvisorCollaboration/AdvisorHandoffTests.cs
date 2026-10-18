using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorHandoffTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid HandoffId = Uuid.CreateVersion4();
    static readonly Uuid LeadId = Uuid.CreateVersion4();
    static readonly Uuid AdvisorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainValidationAsHistoricalGivenASelectedRecordAdvancesToANewVersion()
    {
        // Arrange
        var recordId = Uuid.CreateVersion4();
        var firstVersion = Uuid.CreateVersion4();
        var successorVersion = Uuid.CreateVersion4();
        var handoff = new AdvisorHandoff(TenantId, ProgramId, HandoffId);

        Assert.True(handoff.Create("Type I controls", [new(recordId, firstVersion)],
            new DateOnly(2026, 12, 31), ActorReference.ForMember(LeadId, "Lead"), Now).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1,
            ActorReference.ForMember(LeadId, "Lead"), Now.AddMinutes(1)).IsSuccess);
        Assert.True(handoff.RecordFeedback(Uuid.CreateVersion4(), recordId, firstVersion,
            "validated", "Reviewed against the evidence", ActorReference.ForMember(AdvisorId,
                "Advisor"), Now.AddMinutes(2)).IsSuccess);

        // Act
        Assert.True(handoff.IsValidationCurrent(recordId));
        Assert.True(handoff.ReviseSelection(3, [new(recordId, successorVersion)],
            "The control draft was revised", ActorReference.ForMember(LeadId, "Lead"),
            Now.AddMinutes(3)).IsSuccess);

        // Assert
        Assert.False(handoff.IsValidationCurrent(recordId));
        var feedback = Assert.Single(handoff.ReadFeedback());
        Assert.Equal(firstVersion, feedback.RecordVersionId);
        Assert.Equal(AdvisorFeedbackDisposition.Historical, feedback.Disposition);

        var replay = new AggregateScenario<AdvisorHandoff>(
            new AdvisorHandoff(TenantId, ProgramId, HandoffId)).Given(
                new AggregateScenario<AdvisorHandoff>(handoff).PendingEvents.ToArray());
        Assert.False(replay.Aggregate.IsValidationCurrent(recordId));
        Assert.Equal(firstVersion, Assert.Single(replay.Aggregate.ReadFeedback()).RecordVersionId);
    }

    [Fact]
    public void ShouldRejectFeedbackForAnUnselectedVersionGivenAnOpenReviewHandoff()
    {
        // Arrange
        var recordId = Uuid.CreateVersion4();
        var selectedVersion = Uuid.CreateVersion4();
        var handoff = new AdvisorHandoff(TenantId, ProgramId, HandoffId);
        Assert.True(handoff.Create("Review", [new(recordId, selectedVersion)],
            new DateOnly(2026, 12, 31), ActorReference.ForMember(LeadId, "Lead"), Now).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1,
            ActorReference.ForMember(LeadId, "Lead"), Now.AddMinutes(1)).IsSuccess);
        var pendingBefore = new AggregateScenario<AdvisorHandoff>(handoff).PendingEvents.Count;

        // Act
        var result = handoff.RecordFeedback(Uuid.CreateVersion4(), recordId,
            Uuid.CreateVersion4(), "validated", "Reviewed",
            ActorReference.ForMember(AdvisorId, "Advisor"), Now.AddMinutes(2));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(pendingBefore,
            new AggregateScenario<AdvisorHandoff>(handoff).PendingEvents.Count);
        Assert.Empty(handoff.ReadFeedback());
    }

    [Fact]
    public void ShouldAppendFeedbackCorrectionAndResponseGivenExistingImmutableFeedback()
    {
        // Arrange
        var recordId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var handoff = new AdvisorHandoff(TenantId, ProgramId, HandoffId);
        Assert.True(handoff.Create("Review", [new(recordId, versionId)],
            new DateOnly(2026, 12, 31), ActorReference.ForMember(LeadId, "Lead"), Now).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1,
            ActorReference.ForMember(LeadId, "Lead"), Now.AddMinutes(1)).IsSuccess);
        Assert.True(handoff.RecordFeedback(feedbackId, recordId, versionId,
            "change_request", "Clarify responsibility", ActorReference.ForMember(AdvisorId,
                "Advisor"), Now.AddMinutes(2)).IsSuccess);

        // Act
        Assert.True(handoff.CorrectFeedback(feedbackId, Uuid.CreateVersion4(),
            "Clarify the control owner's responsibility", "The original comment was ambiguous",
            ActorReference.ForMember(AdvisorId, "Advisor"), Now.AddMinutes(3)).IsSuccess);
        Assert.True(handoff.RespondToFeedback(feedbackId, "Updated the draft",
            ActorReference.ForMember(LeadId, "Lead"), Now.AddMinutes(4)).IsSuccess);

        // Assert
        var feedback = Assert.Single(handoff.ReadFeedback());
        Assert.Equal("Clarify responsibility", feedback.Body);
        Assert.Equal("Clarify the control owner's responsibility",
            Assert.Single(feedback.Corrections!).Body);
        Assert.Equal("Updated the draft", Assert.Single(feedback.Responses!).Body);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("revoked")]
    public void ShouldKeepFeedbackHistoryButRejectNewFeedbackGivenHandoffAccessEnds(string ending)
    {
        // Arrange
        var recordId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var handoff = new AdvisorHandoff(TenantId, ProgramId, HandoffId);
        Assert.True(handoff.Create("Review", [new(recordId, versionId)],
            new DateOnly(2026, 12, 31), ActorReference.ForMember(LeadId, "Lead"), Now).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1,
            ActorReference.ForMember(LeadId, "Lead"), Now.AddMinutes(1)).IsSuccess);
        Assert.True(handoff.RecordFeedback(Uuid.CreateVersion4(), recordId, versionId,
            "comment", "Please clarify the control owner", ActorReference.ForMember(AdvisorId,
                "Advisor"), Now.AddMinutes(2)).IsSuccess);

        // Act
        var ended = ending == "closed"
            ? handoff.Close("Review complete", ActorReference.ForMember(LeadId, "Lead"),
                Now.AddMinutes(3))
            : handoff.Revoke("Access removed", ActorReference.ForMember(LeadId, "Lead"),
                Now.AddMinutes(3));
        Assert.True(ended.IsSuccess);

        var rejected = handoff.RecordFeedback(Uuid.CreateVersion4(), recordId, versionId,
            "comment", "Late comment", ActorReference.ForMember(AdvisorId, "Advisor"),
            Now.AddMinutes(4));

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Single(handoff.ReadFeedback());
    }
}
