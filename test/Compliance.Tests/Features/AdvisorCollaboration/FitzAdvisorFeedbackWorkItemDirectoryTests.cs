using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class FitzAdvisorFeedbackWorkItemDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldMoveAttributedFeedbackWorkFromResponseToResolutionGivenOrderedEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var recordVersionId = Uuid.CreateVersion4();
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [new(recordId, recordVersionId)], "Review controls", new DateOnly(2026, 12, 31),
            manager, Now), handoffId, 1);
        var ready = WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId,
            handoffId, 2, manager, Now.AddMinutes(1)), handoffId, 2);
        var feedback = WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
            feedbackId, recordId, recordVersionId, "change_request", "Clarify ownership.",
            author, Now.AddMinutes(2)), handoffId, 3);
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId, created, ready, feedback);
        var recorded = await directory.LoadProgramAsync(tenantId, programId);
        var respondedEvent = WithMetadata(new AdvisorFeedbackResponded(tenantId, programId,
            handoffId, feedbackId, "We updated the control draft.", manager,
            Now.AddMinutes(3)), handoffId, 4);
        await ApplyAsync(directory, tenantId, respondedEvent);
        var responded = await directory.LoadProgramAsync(tenantId, programId);
        var resolvedEvent = WithMetadata(new AdvisorFeedbackResolved(tenantId, programId,
            handoffId, feedbackId, manager, Now.AddMinutes(4)), handoffId, 5);
        await ApplyAsync(directory, tenantId, resolvedEvent);
        var resolved = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(recorded.Value);
        Assert.Equal(WorkSource.AdvisorFeedback, candidate.Kind);
        Assert.Equal(WorkCandidate.IdFor(feedbackId, WorkSource.AdvisorFeedback),
            candidate.WorkItemId);
        Assert.Equal(feedbackId, candidate.SourceId);
        Assert.Equal("Assigned Advisor: Clarify ownership.", candidate.Reason);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            candidate.Responsible);
        Assert.Equal("respond_to_feedback", candidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/responses", candidate.ActionPath,
            StringComparison.Ordinal);
        var responseCandidate = Assert.Single(responded.Value);
        Assert.Equal("resolve_feedback", responseCandidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/resolutions", responseCandidate.ActionPath,
            StringComparison.Ordinal);
        Assert.Empty(resolved.Value);
    }

    [Fact]
    public async Task ShouldExcludeValidationFromActionableWorkGivenFeedbackKinds()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId,
            WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId, [source],
                "Review controls", new DateOnly(2026, 12, 31), manager, Now), handoffId, 1),
            WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId, handoffId, 2,
                manager, Now.AddMinutes(1)), handoffId, 2),
            WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
                Uuid.CreateVersion4(), source.RecordId, source.VersionId, "comment",
                "Clarify ownership.", author, Now.AddMinutes(2)), handoffId, 3),
            WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
                Uuid.CreateVersion4(), source.RecordId, source.VersionId, "validated",
                "The control is well designed.", author, Now.AddMinutes(3)), handoffId, 4));
        var page = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(page.Value);
        Assert.Equal("advisor_feedback", candidate.Kind);
        Assert.Contains("Clarify ownership.", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRetainCorrectedActionableFeedbackGivenHandoffClosure()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());

        // Act
        await ApplyAsync(directory, tenantId,
            WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId, [source],
                "Review controls", new DateOnly(2026, 12, 31), manager, Now), handoffId, 1),
            WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId, handoffId, 2,
                manager, Now.AddMinutes(1)), handoffId, 2),
            WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
                feedbackId, source.RecordId, source.VersionId, "change_request",
                "Clarify ownership.", author, Now.AddMinutes(2)), handoffId, 3),
            WithMetadata(new AdvisorFeedbackCorrected(tenantId, programId, handoffId,
                feedbackId, Uuid.CreateVersion4(), "Clarify the control owner.",
                "The first wording was unclear.", author, Now.AddMinutes(3)), handoffId, 4),
            WithMetadata(new AdvisorHandoffClosed(tenantId, programId, handoffId, 5,
                "Review window ended.", manager, Now.AddMinutes(4)), handoffId, 5));
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
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [source], "Review controls", new DateOnly(2026, 12, 31), manager, Now), handoffId, 1);
        var ready = WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId,
            handoffId, 2, manager, Now.AddMinutes(1)), handoffId, 2);
        var feedback = WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
            feedbackId, source.RecordId, source.VersionId, "comment", "Clarify ownership.",
            author, Now.AddMinutes(2)), handoffId, 3);
        await ApplyAsync(directory, tenantId, created, ready, feedback);

        // Act
        await ApplyAsync(directory, tenantId, feedback);
        var conflictingReplay = WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId,
            handoffId, feedbackId, source.RecordId, source.VersionId, "comment",
            "Different event at the same revision.", author, Now.AddMinutes(2)), handoffId, 3);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApplyAsync(directory, tenantId, conflictingReplay));
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
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var created = WithMetadata(new AdvisorHandoffCreated(foreignTenantId, programId,
            handoffId, [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Foreign handoff",
            new DateOnly(2026, 12, 31), manager, Now), handoffId, 1);
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(created).AsTask());
    }

    [Fact]
    public async Task ShouldRejectFeedbackFromAnotherProgramGivenHandoffProjectionState()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient());
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [source], "Review controls", new DateOnly(2026, 12, 31), manager, Now), handoffId, 1);
        await ApplyAsync(directory, tenantId, created);
        var feedback = WithMetadata(new AdvisorFeedbackRecorded(tenantId, otherProgramId,
            handoffId, feedbackId, source.RecordId, source.VersionId, "change_request",
            "Clarify ownership.", author, Now.AddMinutes(1)), handoffId, 2);
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(feedback).AsTask());
    }

    static async Task ApplyAsync(FitzAdvisorFeedbackWorkItemDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static T WithMetadata<T>(T domainEvent, Uuid handoffId, ulong version) where T : DomainEvent
    {
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), handoffId,
            version, Now));
        return domainEvent;
    }
}
