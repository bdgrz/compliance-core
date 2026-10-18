using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class FitzAdvisorHandoffDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectHandoffLifecycleAndFeedbackTimeGivenOrderedEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var additionalSource = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [source], "Readiness review", new DateOnly(2026, 12, 31), actor, Now), handoffId, 1);
        var revisedAt = Now.AddMinutes(1);
        var revised = WithMetadata(new AdvisorHandoffSelectionRevised(tenantId, programId,
            handoffId, 2, [source, additionalSource], "Clarify the review scope", actor,
            revisedAt), handoffId, 2);
        var readiedAt = Now.AddMinutes(2);
        var readied = WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId,
            handoffId, 3, actor, readiedAt), handoffId, 3);
        var feedbackAt = Now.AddMinutes(3);
        var feedback = WithMetadata(new AdvisorFeedbackRecorded(tenantId, programId, handoffId,
            Uuid.CreateVersion4(), source.RecordId, source.VersionId, "comment",
            "Clarify ownership.", actor, feedbackAt), handoffId, 4);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(revised);
            await directory.ApplyAsync(readied);
            await directory.ApplyAsync(feedback);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(tenantId, programId, 25, null);
        var otherProgram = await directory.ListProgramAsync(tenantId, Uuid.CreateVersion4(), 25, null);
        var otherTenant = await directory.ListProgramAsync(Uuid.CreateVersion4(), programId, 25, null);

        // Assert
        var view = Assert.Single(page.Items);
        Assert.Equal(tenantId, view.TenantId);
        Assert.Equal(programId, view.ProgramId);
        Assert.Equal(handoffId, view.AdvisorRequestId);
        Assert.Equal("Readiness review", view.Name);
        Assert.Equal("ready_for_review", view.Status);
        Assert.Equal(feedbackAt, view.UpdatedAt);
        Assert.Empty(otherProgram.Items);
        Assert.Empty(otherTenant.Items);
    }

    [Fact]
    public async Task ShouldRetainComparisonLineageGivenRepeatedHandoffProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var comparedToId = Uuid.CreateVersion4();
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Follow-up review",
            new DateOnly(2026, 12, 31), ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"),
            Now, comparedToId), handoffId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(tenantId, programId, 25, null);

        // Assert
        Assert.Equal(comparedToId, Assert.Single(page.Items).ComparedToHandoffId);
    }

    [Fact]
    public async Task ShouldRejectLifecycleEventBeforeCreationGivenOutOfOrderProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));
        var revised = WithMetadata(new AdvisorHandoffSelectionRevised(tenantId, programId, handoffId, 2,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Changed selection",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now), handoffId, 2);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(revised).AsTask());
    }

    [Fact]
    public async Task ShouldOrderAndPageProgramHandoffsGivenDifferentUpdateTimes()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var earlierId = Uuid.CreateVersion4();
        var laterId = Uuid.CreateVersion4();
        var earlier = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, earlierId,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Earlier", new DateOnly(2026, 12, 31),
            actor, Now), earlierId, 1);
        var later = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, laterId,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Later", new DateOnly(2026, 12, 31),
            actor, Now.AddMinutes(1)), laterId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(earlier);
            await directory.ApplyAsync(later);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var first = await directory.ListProgramAsync(tenantId, programId, 1, null);
        var second = await directory.ListProgramAsync(tenantId, programId, 1, first.NextCursor);

        // Assert
        Assert.Equal(laterId, Assert.Single(first.Items).AdvisorRequestId);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(earlierId, Assert.Single(second.Items).AdvisorRequestId);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task ShouldRejectReadyEventAfterHandoffExpiryGivenOrderedAggregateEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [source], "Expired review", DateOnly.FromDateTime(Now.UtcDateTime), actor, Now),
            handoffId, 1);
        var readyAt = Now.AddDays(1);
        var ready = WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId,
            handoffId, 2, actor, readyAt), handoffId, 2);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(created);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(ready).AsTask());
    }

    [Fact]
    public async Task ShouldRejectOlderEventReplayGivenLaterAggregateVersionWasProjected()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var created = WithMetadata(new AdvisorHandoffCreated(tenantId, programId, handoffId,
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], "Readiness review",
            new DateOnly(2026, 12, 31), actor, Now), handoffId, 1);
        var ready = WithMetadata(new AdvisorHandoffReadiedForReview(tenantId, programId,
            handoffId, 2, actor, Now.AddMinutes(1)), handoffId, 2);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(created);
        await directory.ApplyAsync(ready);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(created).AsTask());
    }

    [Fact]
    public async Task ShouldRejectForeignTenantHandoffGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var created = WithMetadata(new AdvisorHandoffCreated(foreignTenantId, programId,
            handoffId, [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())],
            "Foreign handoff", new DateOnly(2026, 12, 31),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now), handoffId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => directory.ApplyAsync(created).AsTask());
        Assert.Contains("tenant projection batch", exception.Message, StringComparison.Ordinal);
    }

    static T WithMetadata<T>(T domainEvent, Uuid aggregateId, ulong version) where T : DomainEvent
    {
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId,
            version, Now));
        return domainEvent;
    }
}
