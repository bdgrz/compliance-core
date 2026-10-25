using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class FitzAdvisorFeedbackWorkItemDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    static readonly ActorReference Manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");

    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");

    static AdvisorFeedbackRecorded Recorded(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        AdvisorRecordVersion source, string kind, string body, ulong version = 1) => WithMetadata(
        AdvisorEvents.Recorded(tenantId, programId, handoffId, feedbackId, source, kind, body, Author, Now.AddMinutes(2)),
        tenantId, programId, handoffId, feedbackId, version);

    [Fact]
    public async Task ShouldMoveAttributedFeedbackWorkFromResponseToResolutionGivenOrderedEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var feedback = Recorded(tenantId, programId, handoffId, feedbackId, source, "change_request",
            "Clarify ownership.");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId, feedback);
        var recorded = await directory.LoadProgramAsync(tenantId, programId);
        var respondedEvent = WithMetadata(AdvisorEvents.Responded(tenantId, programId, handoffId, feedbackId,
            "We updated the control draft.", Manager, Now.AddMinutes(3)), tenantId, programId, handoffId, feedbackId, 2);
        await ApplyAsync(directory, tenantId, respondedEvent);
        var responded = await directory.LoadProgramAsync(tenantId, programId);
        var resolvedEvent = WithMetadata(AdvisorEvents.Resolved(tenantId, programId, handoffId, feedbackId, Manager,
            Now.AddMinutes(4)), tenantId, programId, handoffId, feedbackId, 3);
        await ApplyAsync(directory, tenantId, resolvedEvent);
        var resolved = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(recorded.Value);
        Assert.Equal(WorkSource.AdvisorFeedback, candidate.Kind);
        Assert.Equal(WorkCandidate.IdFor(AdvisorFeedbackThread.IdentityOf(tenantId, programId, handoffId, feedbackId),
            WorkSource.AdvisorFeedback), candidate.WorkItemId);
        Assert.Equal(feedbackId, candidate.SourceId);
        Assert.Equal("Assigned Advisor: Clarify ownership.", candidate.Reason);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId), candidate.Responsible);
        Assert.Equal("respond_to_feedback", candidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/responses", candidate.ActionPath, StringComparison.Ordinal);
        var responseCandidate = Assert.Single(responded.Value);
        Assert.Equal("resolve_feedback", responseCandidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/resolutions", responseCandidate.ActionPath, StringComparison.Ordinal);
        Assert.Empty(resolved.Value);
    }

    [Fact]
    public async Task ShouldExcludeValidationFromActionableWorkGivenFeedbackKinds()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId,
            Recorded(tenantId, programId, handoffId, Uuid.CreateVersion4(), source, "comment", "Clarify ownership."),
            Recorded(tenantId, programId, handoffId, Uuid.CreateVersion4(), source, "validated",
                "The control is well designed."));
        var page = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(page.Value);
        Assert.Equal("advisor_feedback", candidate.Kind);
        Assert.Contains("Clarify ownership.", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRetainCorrectedActionableFeedbackGivenLaterCorrection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId,
            Recorded(tenantId, programId, handoffId, feedbackId, source, "change_request", "Clarify ownership."),
            WithMetadata(AdvisorEvents.Corrected(tenantId, programId, handoffId, feedbackId,
                "Clarify the control owner.", "The first wording was unclear.", Author, Now.AddMinutes(3)),
                tenantId, programId, handoffId, feedbackId, 2));
        var work = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(work.Value);
        Assert.Contains("Clarify the control owner.", candidate.Reason, StringComparison.Ordinal);
        Assert.Equal("respond_to_feedback", candidate.NextAction);
    }

    [Fact]
    public async Task ShouldIgnoreExactReplayAndRejectConflictingReplayGivenProjectedFeedback()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        var feedback = Recorded(tenantId, programId, handoffId, feedbackId, source, "comment", "Clarify ownership.");
        await ApplyAsync(directory, tenantId, feedback);

        // Act
        await ApplyAsync(directory, tenantId, feedback);
        var conflictingReplay = Recorded(tenantId, programId, handoffId, feedbackId, source, "comment",
            "Different event at the same revision.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(directory, tenantId, conflictingReplay));
        var work = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(work.Value);
        Assert.Contains("Clarify ownership.", candidate.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Different event", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectForeignTenantEventGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var foreign = Recorded(foreignTenantId, programId, handoffId, feedbackId, source, "comment", "Foreign.");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(foreign).AsTask());
    }

    [Fact]
    public async Task ShouldRejectFollowUpFromAnotherProgramGivenThreadProjectionState()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        await ApplyAsync(directory, tenantId,
            Recorded(tenantId, programId, handoffId, feedbackId, source, "change_request", "Clarify ownership."));
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));
        var foreignFollowUp = WithMetadata(AdvisorEvents.Responded(tenantId, otherProgramId, handoffId, feedbackId,
            "Update.", Manager, Now.AddMinutes(3)), tenantId, programId, handoffId, feedbackId, 2);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(foreignFollowUp).AsTask());
    }

    static async Task ApplyAsync(FitzAdvisorFeedbackWorkItemDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static T WithMetadata<T>(T domainEvent, Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        ulong version) where T : DomainEvent
    {
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            AdvisorFeedbackThread.IdentityOf(tenantId, programId, handoffId, feedbackId), version, Now));
        return domainEvent;
    }
}
