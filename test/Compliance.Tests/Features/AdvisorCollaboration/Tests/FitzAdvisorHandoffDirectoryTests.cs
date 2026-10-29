using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

public sealed class FitzAdvisorHandoffDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectHandoffLifecycleAndFeedbackTimeGivenOrderedEvents()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var additionalSource = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var created = WithMetadata(await AdvisorEvents.Created(content, [source], tenantId, programId, handoffId, "Readiness review",
            new DateOnly(2026, 12, 31), actor, Now), handoffId, 1);
        var revisedAt = Now.AddMinutes(1);
        var revised = WithMetadata(await AdvisorEvents.Revised(content, [source, additionalSource], tenantId, programId, handoffId, 2,
            "Clarify the review scope", actor, revisedAt), handoffId, 2);
        var readiedAt = Now.AddMinutes(2);
        var readied = WithMetadata(AdvisorEvents.Readied(tenantId, programId, handoffId, 3, actor, readiedAt),
            handoffId, 3);
        var feedbackAt = Now.AddMinutes(3);
        var feedback = WithMetadata(AdvisorEvents.Admitted(tenantId, handoffId, Uuid.CreateVersion4(), 4, 2, actor,
            feedbackAt), handoffId, 4);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created, [source]);
            await directory.ApplyAsync(created, [source]);
            await directory.ApplyAsync(revised, [source, additionalSource]);
            await directory.ApplyAsync(readied, null);
            await directory.ApplyAsync(feedback, null);
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
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var comparedToId = Uuid.CreateVersion4();
        var created = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, handoffId, "Follow-up review",
            new DateOnly(2026, 12, 31), ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now, comparedToId),
            handoffId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created, selection);
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
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));
        var revised = WithMetadata(await AdvisorEvents.Revised(content, selection, tenantId, programId, handoffId, 2, "Changed selection",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now), handoffId, 2);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(revised, selection).AsTask());
    }

    [Fact]
    public async Task ShouldOrderAndPageProgramHandoffsGivenDifferentUpdateTimes()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var earlierId = Uuid.CreateVersion4();
        var laterId = Uuid.CreateVersion4();
        var earlier = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, earlierId, "Earlier",
            new DateOnly(2026, 12, 31), actor, Now), earlierId, 1);
        var later = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, laterId, "Later",
            new DateOnly(2026, 12, 31), actor, Now.AddMinutes(1)), laterId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(earlier, selection);
            await directory.ApplyAsync(later, selection);
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
    public async Task ShouldListOnlyUnexpiredReadyHandoffsGivenSharedProgramQuery()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var draftId = Uuid.CreateVersion4();
        var expiredId = Uuid.CreateVersion4();
        var currentId = Uuid.CreateVersion4();
        var draftCreated = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, draftId, "Unshared draft",
            new DateOnly(2026, 12, 31), actor, Now.AddDays(-1)), draftId, 1);
        var expiredCreated = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, expiredId, "Expired handoff",
            new DateOnly(2026, 10, 18), actor, Now.AddDays(-1)), expiredId, 1);
        var expiredReady = WithMetadata(AdvisorEvents.Readied(tenantId, programId, expiredId, 2, actor,
            Now.AddMinutes(2)), expiredId, 2);
        var currentCreated = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, currentId, "Current handoff",
            new DateOnly(2026, 12, 31), actor, Now.AddDays(-1)), currentId, 1);
        var currentReady = WithMetadata(AdvisorEvents.Readied(tenantId, programId, currentId, 2, actor,
            Now.AddMinutes(1)), currentId, 2);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(draftCreated, selection);
            await directory.ApplyAsync(expiredCreated, selection);
            await directory.ApplyAsync(expiredReady, null);
            await directory.ApplyAsync(currentCreated, selection);
            await directory.ApplyAsync(currentReady, null);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListSharedProgramAsync(tenantId, programId,
            new DateOnly(2026, 10, 19), 1, null);

        // Assert
        Assert.Equal(currentId, Assert.Single(page.Items).AdvisorRequestId);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldRejectReadyEventAfterHandoffExpiryGivenOrderedAggregateEvents()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var created = WithMetadata(await AdvisorEvents.Created(content, [source], tenantId, programId, handoffId, "Expired review",
            DateOnly.FromDateTime(Now.UtcDateTime), actor, Now), handoffId, 1);
        var readyAt = Now.AddDays(1);
        var ready = WithMetadata(AdvisorEvents.Readied(tenantId, programId, handoffId, 2, actor, readyAt),
            handoffId, 2);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(created, [source]);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(ready, null).AsTask());
    }

    [Fact]
    public async Task ShouldRejectOlderEventReplayGivenLaterAggregateVersionWasProjected()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var created = WithMetadata(await AdvisorEvents.Created(content, selection, tenantId, programId, handoffId, "Readiness review",
            new DateOnly(2026, 12, 31), actor, Now), handoffId, 1);
        var ready = WithMetadata(AdvisorEvents.Readied(tenantId, programId, handoffId, 2, actor,
            Now.AddMinutes(1)), handoffId, 2);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(created, selection);
        await directory.ApplyAsync(ready, null);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(created, selection).AsTask());
    }

    [Fact]
    public async Task ShouldRejectForeignTenantHandoffGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var selection = new[] { AnySource() };
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var created = WithMetadata(await AdvisorEvents.Created(content, selection, foreignTenantId, programId, handoffId, "Foreign handoff",
            new DateOnly(2026, 12, 31), ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now), handoffId, 1);
        var directory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorHandoffDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => directory.ApplyAsync(created, selection).AsTask());
        Assert.Contains("tenant projection batch", exception.Message, StringComparison.Ordinal);
    }

    static AdvisorRecordVersion AnySource() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static T WithMetadata<T>(T domainEvent, Uuid aggregateId, ulong version) where T : DomainEvent
    {
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), aggregateId,
            version, Now));
        return domainEvent;
    }
}
