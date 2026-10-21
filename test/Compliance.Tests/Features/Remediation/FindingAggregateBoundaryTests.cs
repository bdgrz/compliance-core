using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingAggregateBoundaryTests
{
    [Fact]
    public void ShouldTreatExactRaiseRetryAsSuccessAndRejectConflictingReuseGivenFindingWasRaised()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var finding = new FindingAggregate(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());
        var source = new FindingSource("manual", null, null, "Gap.");
        var owner = Uuid.CreateVersion4();
        var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(10);

        // Act
        var first = finding.Raise(source, "Gap", "Gap found.", "medium", "All", owner,
            dueOn, [], actor, now);
        var retry = finding.Raise(source, "Gap", "Gap found.", "medium", "All", owner,
            dueOn, [], actor, now);
        var conflictingReuse = finding.Raise(source, "Different title", "Gap found.",
            "medium", "All", owner, dueOn, [], actor, now);

        // Assert
        Assert.Null(first);
        Assert.Null(retry);
        Assert.NotNull(conflictingReuse);
    }

    [Fact]
    public void ShouldRejectPartitioningFenceWithWrongSourceVersionGivenLegacyHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var raised = DomainEventSeed.Attach(new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.", "medium",
            "All", Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(10),
            [], actor, now), programId, 1, occurredOn: now);
        var misplacedFence = DomainEventSeed.Attach(new RemediationPartitioningStarted(tenantId,
            programId, migrationId, 0, ActorReference.ForSystemProcess("migration",
                "finding stream migration"), now.AddSeconds(1)), programId, 2,
            occurredOn: now.AddSeconds(1));
        var ledger = new AggregateScenario<RemediationLedger>(new RemediationLedger(tenantId,
            programId));

        // Act
        var replay = () => ledger.Given(raised, misplacedFence);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Theory]
    [InlineData("raise")]
    [InlineData("revise")]
    [InlineData("add_action")]
    [InlineData("complete_action")]
    [InlineData("link_acceptance")]
    [InlineData("close")]
    [InlineData("reopen")]
    public void ShouldRejectEveryLegacyWriteGivenFindingStreamsWereFencedForMigration(
        string operation)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var raised = DomainEventSeed.Attach(new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.", "medium",
            "All", Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(10),
            [], actor, now), programId, 1, occurredOn: now);
        var started = DomainEventSeed.Attach(new RemediationPartitioningStarted(tenantId,
            programId, migrationId, 1, ActorReference.ForSystemProcess("migration",
                "finding stream migration"), now), programId, 2, occurredOn: now);
        var ledger = new AggregateScenario<RemediationLedger>(new RemediationLedger(tenantId,
            programId)).Given(raised, started).Aggregate;
        var ownerId = Uuid.CreateVersion4();
        var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(14);

        // Act
        var result = operation switch
        {
            "raise" => ledger.Raise(Uuid.CreateVersion4(),
                new FindingSource("manual", null, null, "Another gap."), "Another gap.",
                "Another gap.", "medium", "All", ownerId, dueOn, [], actor, now),
            "revise" => ledger.Revise(findingId, 1, "high", ownerId, dueOn.AddDays(1),
                "All", null, "Change the triage.", actor, now),
            "add_action" => ledger.AddAction(findingId, 1, Uuid.CreateVersion4(),
                "Restore expected access.", ownerId, dueOn, actor, now),
            "complete_action" => ledger.CompleteAction(findingId, 1, Uuid.CreateVersion4(),
                "Resolved with review.", [], ownerId, actor, now),
            "link_acceptance" => ledger.LinkAcceptance(findingId, 1, "risk_acceptance",
                Uuid.CreateVersion4(), null, now.AddDays(1), actor, now),
            "close" => ledger.Close(findingId, 1, Uuid.CreateVersion4(),
                "The evidence supports the resolution.", [], "Verified remediation.", ownerId,
                actor, now, null),
            "reopen" => ledger.Reopen(findingId, 1, "The issue returned.", actor, now),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation,
                "Unknown legacy write operation."),
        };

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2UL, ledger.Version);
    }

    [Fact]
    public void ShouldKeepPreFenceCutoffSeparateFromMarkerAndCursorAuditsGivenMigrationRetries()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = Raised(tenantId, programId, findingId, actor, now, 1);
        var sourceFact = WithMetadata(source, source.Metadata with
        {
            AggregateId = programId,
            AggregateVersion = 12,
        });
        var stream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        var factRecord = new DomainEventRecord(stream, sourceFact, 0, EventCursor.Start);
        var auditEvent = WithMetadata(sourceFact, sourceFact.Metadata with { IsAudit = true });
        var auditRecord = new DomainEventRecord(stream, auditEvent, 1, EventCursor.Start);
        var marker = DomainEventSeed.Attach(new RemediationPartitioningStarted(tenantId,
            programId, migrationId, 12,
            ActorReference.ForSystemProcess("migration", "finding stream migration"),
            now.AddSeconds(1)), programId, 13, occurredOn: now.AddSeconds(1));
        var markerRecord = new DomainEventRecord(stream, marker, 2, EventCursor.Start);
        var laterFact = WithMetadata(source with { Revision = 2 }, source.Metadata with
        {
            AggregateId = programId,
            AggregateVersion = 14,
            EventId = Uuid.CreateVersion4(),
            OccurredOn = now.AddSeconds(2),
        });
        var laterRecord = new DomainEventRecord(stream, laterFact, 3, EventCursor.Start);

        // Act
        var fence = FindingMigrationFence.FromMarker(markerRecord);
        var retryFence = FindingMigrationFence.FromMarker(markerRecord);
        var futureAudit = WithMetadata(auditEvent, auditEvent.Metadata with
        {
            EventId = Uuid.CreateVersion4(),
            AggregateVersion = 13,
        });
        var futureAuditRecord = new DomainEventRecord(stream, futureAudit, 1,
            EventCursor.Start);

        // Assert
        Assert.Equal(fence, retryFence);
        Assert.Equal(12UL, fence.PreFenceProgramVersion);
        Assert.Equal(2UL, fence.MarkerResourceOffset);
        Assert.Equal(13UL, fence.MarkerProgramVersion);
        Assert.True(fence.IsBeforeMarker(factRecord));
        Assert.True(fence.IsImportableFact(factRecord));
        Assert.True(fence.IsBeforeMarker(auditRecord));
        Assert.False(fence.IsImportableFact(auditRecord));
        Assert.Throws<InvalidOperationException>(() => fence.IsBeforeMarker(futureAuditRecord));
        Assert.True(fence.IsMarker(markerRecord));
        Assert.False(fence.IsBeforeMarker(markerRecord));
        Assert.False(fence.IsImportableFact(markerRecord));
        Assert.False(fence.IsBeforeMarker(laterRecord));
        Assert.False(fence.IsImportableFact(laterRecord));
    }

    [Fact]
    public void ShouldAdvanceOnePhysicalSourceOffsetBeforeSealingGivenFenceSnapshot()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForSystemProcess("migration", "finding stream migration");
        var findingEvent = Raised(tenantId, programId, findingId,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), now, 1);
        var legacyEvent = WithMetadata(findingEvent, findingEvent.Metadata with
        {
            AggregateId = programId,
            AggregateVersion = 12,
        });
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        var sourceFact = new DomainEventRecord(sourceStream, legacyEvent, 0, EventCursor.Start);
        var auditEvent = WithMetadata(legacyEvent, legacyEvent.Metadata with
        {
            EventId = Uuid.CreateVersion4(),
            IsAudit = true,
        });
        var sourceAudit = new DomainEventRecord(sourceStream, auditEvent, 1, EventCursor.Start);
        var fenceEvent = DomainEventSeed.Attach(new RemediationPartitioningStarted(tenantId,
            programId, migrationId, 12, actor, now), programId, 13, occurredOn: now);
        var fenceRecord = new DomainEventRecord(sourceStream, fenceEvent, 2, EventCursor.Start);
        var fence = FindingMigrationFence.FromMarker(fenceRecord);
        var coordinator = new FindingMigrationAggregate(tenantId, programId);
        var malformedStart = DomainEventSeed.Attach(new FindingMigrationStarted(tenantId,
            programId, migrationId, fence.MarkerEventId, fence.PreFenceProgramVersion,
            ulong.MaxValue, fence.MarkerProgramVersion, actor, now), programId, 1,
            occurredOn: now);

        // Act
        var startResult = coordinator.Start(fence, actor, now);
        var started = Assert.Single(new AggregateScenario<FindingMigrationAggregate>(coordinator)
            .PendingEvents);
        var startedCoordinator = new AggregateScenario<FindingMigrationAggregate>(
            new FindingMigrationAggregate(tenantId, programId)).Given(started).Aggregate;
        var factResult = startedCoordinator.Advance(sourceFact, now.AddSeconds(1));
        var factProgress = Assert.Single(new AggregateScenario<FindingMigrationAggregate>(
            startedCoordinator).PendingEvents);
        var afterFact = new AggregateScenario<FindingMigrationAggregate>(
            new FindingMigrationAggregate(tenantId, programId)).Given(started,
                factProgress).Aggregate;
        var duplicateResult = afterFact.Advance(sourceFact, now.AddSeconds(2));
        var auditResult = afterFact.Advance(sourceAudit, now.AddSeconds(2));
        var auditProgress = Assert.Single(new AggregateScenario<FindingMigrationAggregate>(afterFact)
            .PendingEvents);
        var afterAudit = new AggregateScenario<FindingMigrationAggregate>(
            new FindingMigrationAggregate(tenantId, programId)).Given(started, factProgress,
                auditProgress).Aggregate;
        var markerAdvance = afterAudit.Advance(fenceRecord, now.AddSeconds(3));
        var invalidActorCompletion = afterAudit.Complete(fence.MarkerEventId,
            fence.MarkerResourceOffset, afterAudit.ImportedFindingEventCount,
            afterAudit.SourceDigest, null!, now.AddSeconds(3));
        var invalidTimeCompletion = afterAudit.Complete(fence.MarkerEventId,
            fence.MarkerResourceOffset, afterAudit.ImportedFindingEventCount,
            afterAudit.SourceDigest, actor, default);
        var completeResult = afterAudit.Complete(fence.MarkerEventId,
            fence.MarkerResourceOffset, afterAudit.ImportedFindingEventCount,
            afterAudit.SourceDigest, actor, now.AddSeconds(3));
        var malformedCompleted = DomainEventSeed.Attach(new FindingMigrationCompleted(tenantId,
            programId, migrationId, fence.MarkerEventId, fence.MarkerResourceOffset,
            afterAudit.ImportedFindingEventCount, afterAudit.SourceDigest, null!,
            now.AddSeconds(3)), programId, 4, occurredOn: now.AddSeconds(3));

        // Assert
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<
            FindingMigrationAggregate>(new FindingMigrationAggregate(tenantId, programId))
            .Given(malformedStart));
        Assert.Null(startResult);
        Assert.Null(factResult);
        Assert.Null(duplicateResult);
        Assert.Null(auditResult);
        Assert.NotNull(markerAdvance);
        Assert.NotNull(invalidActorCompletion);
        Assert.NotNull(invalidTimeCompletion);
        Assert.Null(completeResult);
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<
            FindingMigrationAggregate>(new FindingMigrationAggregate(tenantId, programId))
            .Given(started, factProgress, auditProgress, malformedCompleted));
        Assert.Equal(2UL, afterAudit.NextSourceOffset);
        Assert.Equal(1UL, afterAudit.ImportedFindingEventCount);
        Assert.True(FindingMigrationDigest.IsDigest(afterAudit.SourceDigest));
    }

    [Fact]
    public void ShouldUseOneTenantScopedStreamPerFindingGivenIndependentFindings()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstFindingId = Uuid.CreateVersion4();
        var secondFindingId = Uuid.CreateVersion4();

        // Act
        var first = new FindingAggregate(tenantId, programId, firstFindingId);
        var second = new FindingAggregate(tenantId, programId, secondFindingId);
        var otherTenant = new FindingAggregate(Uuid.CreateVersion4(), programId, firstFindingId);

        // Assert
        Assert.Equal(FindingAggregate.StreamId(programId, firstFindingId), first.Id);
        Assert.Equal(new EventStreamAddress(tenantId.ToString(), FindingAggregate.Area,
            FindingAggregate.StreamId(programId, firstFindingId).ToString()), first.Stream);
        Assert.NotEqual(first.Stream, second.Stream);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
        Assert.NotEqual(first.Stream, new FindingAggregate(tenantId,
            Uuid.CreateVersion4(), firstFindingId).Stream);
    }

    [Fact]
    public async Task ShouldRejectAcceptanceDecisionGivenHistoryFoldBeforeConcurrentAppend()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var finding = new FindingAggregate(tenantId, programId, findingId);
        var store = new InMemoryEventStore();
        var staleHistory = await FindingHistoryFold.ReadAsync(store, tenantId, programId,
            findingId, CancellationToken.None);
        var raised = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.", "medium",
            "All", Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(10),
            [], actor, now);
        var committedRaise = DomainEventSeed.Attach(raised, finding.Id, 1, occurredOn: now);
        await store.AppendAsync(finding.Stream, 0, [committedRaise]);
        finding = new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
                programId, findingId))
            .Given(committedRaise)
            .Aggregate;
        var hydratedVersion = finding.Version;
        Assert.Equal(1UL, hydratedVersion);

        // Act
        var result = finding.LinkAcceptance(staleHistory, 1, "waiver", Uuid.CreateVersion4(),
            null, now.AddDays(30), actor, now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(hydratedVersion, finding.Version);
        var view = finding.Read(new FindingHistoryFold(hydratedVersion, [],
            [new FindingHistoryEntryView(1, "raised", "Gap.", actor, now)], []), now);
        Assert.NotNull(view);
        Assert.Equal(["raised"], view.History.Select(entry => entry.Change));
    }

    [Fact]
    public void ShouldRejectSecondRaiseGivenFindingRevisionIsContiguous()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var first = Raised(tenantId, programId, findingId, actor, now, 1);
        var second = DomainEventSeed.Attach(first with { Revision = 2, Title = "Second raise" },
            FindingAggregate.StreamId(programId, findingId), 2, occurredOn: now.AddSeconds(1));

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(first, second);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectRepeatedActionCompletionGivenRevisionIsContiguous()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(owner,
            "Owner");
        var aggregateId = FindingAggregate.StreamId(programId, findingId);
        var raised = Raised(tenantId, programId, findingId, actor, now, 1);
        var action = DomainEventSeed.Attach(new CorrectiveActionAdded(tenantId, programId,
            findingId, 2, new CorrectiveActionView(actionId, "Fix", owner,
                DateOnly.FromDateTime(now.UtcDateTime).AddDays(5), FindingAggregate.Open, actor,
                now)), aggregateId, 2, occurredOn: now.AddSeconds(1));
        var completed = DomainEventSeed.Attach(new CorrectiveActionCompleted(tenantId, programId,
            findingId, 3, actionId, "Fixed", [], owner, actor, now.AddSeconds(2)), aggregateId, 3,
            occurredOn: now.AddSeconds(2));
        var repeated = DomainEventSeed.Attach(completed with { Revision = 4 }, aggregateId, 4,
            occurredOn: now.AddSeconds(3));

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(raised, action, completed, repeated);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectClosureWithoutCompletedWorkGivenFindingWasRaised()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var aggregateId = FindingAggregate.StreamId(programId, findingId);
        var raised = Raised(tenantId, programId, findingId, actor, now, 1);
        var closure = DomainEventSeed.Attach(new FindingClosed(tenantId, programId, findingId, 2,
            new FindingClosureView(Uuid.CreateVersion4(), "Verified", [], "Closed",
                Uuid.CreateVersion4(), actor, now.AddSeconds(1))), aggregateId, 2,
            occurredOn: now.AddSeconds(1));

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(raised, closure);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldRejectReopenWithoutClosureGivenFindingWasRaised()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var aggregateId = FindingAggregate.StreamId(programId, findingId);
        var raised = Raised(tenantId, programId, findingId, actor, now, 1);
        var reopened = DomainEventSeed.Attach(new FindingReopened(tenantId, programId, findingId,
            2, "Reopen", actor, now.AddSeconds(1)), aggregateId, 2,
            occurredOn: now.AddSeconds(1));

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(raised, reopened);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldPreserveLegacySourceLineageGivenFindingEventIsImported()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var sourceEvent = Raised(tenantId, programId, findingId, actor, now, 1);
        var sourceRecord = LegacyRecord(tenantId, programId, sourceEvent, 12,
            now.AddSeconds(1));
        var producer = new FindingAggregate(tenantId, programId, findingId);
        var store = new InMemoryEventStore();
        Assert.Null(producer.ImportLegacy(sourceRecord));
        var pair = new AggregateScenario<FindingAggregate>(producer).PendingEvents.ToArray();
        await store.AppendAsync(producer.Stream, 0, pair);

        var finding = new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(pair).Aggregate;

        // Act
        var history = await FindingHistoryFold.ReadAsync(store, tenantId, programId, findingId,
            CancellationToken.None);
        finding.EnsureImportComplete();

        // Assert
        Assert.Equal(sourceRecord.Event.Metadata.EventId,
            history.SourceLineage.Single().SourceEventId);
        Assert.Equal(12UL, history.SourceLineage.Single().SourceProgramStreamVersion);
        Assert.Equal(now.AddSeconds(1), history.SourceLineage.Single().SourceOccurredAt);
        Assert.Equal(2UL, history.SourceLineage.Single().TargetStreamVersion);
        Assert.Equal(["raised"], history.History.Select(entry => entry.Change));
        Assert.Equal(now, history.History.Single().At);
        Assert.Equal("Gap", finding.Read(history, now)?.Title);
    }

    [Fact]
    public void ShouldRejectOutOfOrderLegacyImportGivenTargetHistoryWasReplayed()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var first = Raised(tenantId, programId, findingId, actor, now, 1);
        var second = new FindingRevised(tenantId, programId, findingId, 2, "high",
            Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(20),
            "Scope", "Cause", "Reason", actor, now.AddSeconds(1));
        var firstPair = ImportPair(tenantId, programId, findingId, first,
            Uuid.CreateVersion4(), 12, 1, now);
        var secondPair = ImportPair(tenantId, programId, findingId, second,
            Uuid.CreateVersion4(), 11, 3, now.AddSeconds(1));

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given([.. firstPair, .. secondPair]);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldTreatLatestLegacyImportRetryAsIdempotentGivenAppendWasCommitted()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var sourceEvent = Raised(tenantId, programId, findingId, actor, now, 1);
        var sourceRecord = LegacyRecord(tenantId, programId, sourceEvent, 12, now);
        var sourceEventId = sourceRecord.Event.Metadata.EventId;
        var finding = new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(ImportPair(tenantId, programId, findingId,
                sourceEvent, sourceEventId, 12, 1, now)).Aggregate;

        // Act
        var retry = finding.ImportLegacy(sourceRecord);

        // Assert
        Assert.Null(retry);
        Assert.Equal(2UL, finding.Version);
    }

    [Fact]
    public void ShouldImportAdmissibleLegacyFactLargerThanTargetPayloadCapGivenFindingWasRaised()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var source = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, new string('s', 6000)),
            new string('t', 200), new string('d', 6000), "medium", new string('a', 2000),
            Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(10),
            Enumerable.Range(0, 42).Select(index => new FindingLink("access_decision",
                new string(index < 5 ? 'é' : 'r', 500))).ToArray(), actor, now);
        var sourcePayloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(source,
            ComplianceCoreJsonContext.Default.FindingRaised).Length;
        Assert.True(sourcePayloadBytes > 48 * 1024,
            $"The source payload was only {sourcePayloadBytes} bytes.");
        Assert.True(sourcePayloadBytes < 60 * 1024,
            $"The source payload was {sourcePayloadBytes} bytes, too close to Fitz's frame limit.");
        var sourceRecord = LegacyRecord(tenantId, programId, source, 1, now);
        var finding = new FindingAggregate(tenantId, programId, findingId);

        // Act
        var result = finding.ImportLegacy(sourceRecord);

        // Assert
        Assert.Null(result);
        var imported = new AggregateScenario<FindingAggregate>(finding).PendingEvents;
        Assert.Equal(2, imported.Count);
        var marker = Assert.IsType<FindingHistoryEventImported>(imported[0]);
        Assert.Equal(FindingHistoryEventPayloads.Hash(System.Text.Json.JsonSerializer
            .SerializeToElement(source, ComplianceCoreJsonContext.Default.FindingRaised)),
            marker.SourceEventSha256);
        var importedFact = Assert.IsType<FindingRaised>(imported[1]);
        Assert.Equal(System.Text.Json.JsonSerializer.SerializeToElement(source,
                ComplianceCoreJsonContext.Default.FindingRaised).GetRawText(),
            System.Text.Json.JsonSerializer.SerializeToElement(importedFact,
                ComplianceCoreJsonContext.Default.FindingRaised).GetRawText());
        finding.EnsureImportComplete();
    }

    [Fact]
    public void ShouldRejectMismatchedLegacyStreamOrOffsetGivenSourceRecordIsImported()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var source = Raised(tenantId, programId, findingId, actor, now, 1);
        var valid = LegacyRecord(tenantId, programId, source, 12, now);
        var wrongStream = valid with
        {
            Stream = new EventStreamAddress(tenantId.ToString(), "other",
                programId.ToString()),
        };
        var wrongOffset = valid with { ResourceOffset = 10 };
        var missingEventId = valid with
        {
            Event = WithMetadata(valid.Event, valid.Event.Metadata with { EventId = Uuid.Empty }),
        };
        var missingOccurredAt = valid with
        {
            Event = WithMetadata(valid.Event, valid.Event.Metadata with { OccurredOn = default }),
        };

        // Act
        var streamFailure = new FindingAggregate(tenantId, programId, findingId)
            .ImportLegacy(wrongStream);
        var offsetFailure = new FindingAggregate(tenantId, programId, findingId)
            .ImportLegacy(wrongOffset);
        var eventIdFailure = new FindingAggregate(tenantId, programId, findingId)
            .ImportLegacy(missingEventId);
        var occurredAtFailure = new FindingAggregate(tenantId, programId, findingId)
            .ImportLegacy(missingOccurredAt);

        // Assert
        Assert.NotNull(streamFailure);
        Assert.NotNull(offsetFailure);
        Assert.NotNull(eventIdFailure);
        Assert.NotNull(occurredAtFailure);
    }

    [Fact]
    public async Task ShouldUseSourceAggregateVersionGivenAuditsOccupyLegacyPhysicalOffsets()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var source = Raised(tenantId, programId, findingId, actor, now, 12);
        var sourceRecord = LegacyRecord(tenantId, programId, source, 12, now) with
        {
            ResourceOffset = 17,
        };
        var finding = new FindingAggregate(tenantId, programId, findingId);

        // Act
        var result = finding.ImportLegacy(sourceRecord);
        var imported = new AggregateScenario<FindingAggregate>(finding).PendingEvents.ToArray();
        var store = new InMemoryEventStore();
        await store.AppendAsync(finding.Stream, 0, imported);
        var history = await FindingHistoryFold.ReadAsync(store, tenantId, programId, findingId,
            CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(2, imported.Length);
        var marker = Assert.IsType<FindingHistoryEventImported>(imported[0]);
        Assert.IsType<FindingRaised>(imported[1]);
        Assert.Equal(17UL, marker.SourceProgramStreamOffset);
        Assert.Equal(12UL, marker.SourceProgramStreamVersion);
        Assert.Equal(17UL, history.SourceLineage.Single().SourceProgramStreamOffset);
        Assert.Equal(12UL, history.SourceLineage.Single().SourceProgramStreamVersion);
    }

    [Fact]
    public async Task ShouldRejectOrphanImportMarkerGivenFindingStreamEndsBeforePairedFact()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var sourceEvent = Raised(tenantId, programId, findingId, actor, now, 1);
        var marker = ImportPair(tenantId, programId, findingId, sourceEvent,
            Uuid.CreateVersion4(), 12, 1, now)[0];
        var store = new InMemoryEventStore();
        var stream = new FindingAggregate(tenantId, programId, findingId).Stream;
        await store.AppendAsync(stream, 0, [marker]);
        var finding = new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(marker).Aggregate;

        // Act
        var aggregateRejects = () => finding.EnsureImportComplete();
        var foldRejects = () => FindingHistoryFold.ReadAsync(store, tenantId, programId,
            findingId, CancellationToken.None).AsTask();

        // Assert
        Assert.Throws<InvalidOperationException>(aggregateRejects);
        await Assert.ThrowsAsync<InvalidOperationException>(foldRejects);
    }

    [Fact]
    public void ShouldRejectMismatchedImportedFactGivenMarkerWasReplayed()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
            Uuid.CreateVersion4(), "Lead");
        var sourceEvent = Raised(tenantId, programId, findingId, actor, now, 1);
        var pair = ImportPair(tenantId, programId, findingId, sourceEvent,
            Uuid.CreateVersion4(), 12, 1, now);
        var revised = new FindingRevised(tenantId, programId, findingId, 1, "high",
            Uuid.CreateVersion4(), DateOnly.FromDateTime(now.UtcDateTime).AddDays(20),
            "Scope", "Cause", "Reason", actor, now);
        var mismatchedFact = DomainEventSeed.Attach(revised,
            FindingAggregate.StreamId(programId, findingId), 2, occurredOn: now);

        // Act
        var replay = () => new AggregateScenario<FindingAggregate>(new FindingAggregate(tenantId,
            programId, findingId)).Given(pair[0], mismatchedFact);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    static DomainEvent[] ImportPair(Uuid tenantId, Uuid programId, Uuid findingId,
        DomainEvent sourceEvent, Uuid sourceEventId, ulong sourceVersion, ulong targetVersion,
        DateTimeOffset targetOccurredAt)
    {
        Assert.True(FindingHistoryEventPayloads.TryCreate(sourceEvent, tenantId, programId,
            findingId, out var payload));
        var targetId = FindingAggregate.StreamId(programId, findingId);
        var marker = DomainEventSeed.Attach(new FindingHistoryEventImported(tenantId, programId,
            findingId, payload.Revision, sourceEventId, sourceVersion - 1, sourceVersion,
            payload.Kind,
            FindingHistoryEventPayloads.Hash(payload.Json), payload.OccurredAt), targetId,
            targetVersion, occurredOn: targetOccurredAt);
        var fact = DomainEventSeed.Attach(FindingHistoryEventPayloads.Deserialize(payload.Kind,
            payload.Json), targetId, targetVersion + 1, occurredOn: targetOccurredAt);
        return [marker, fact];
    }

    static DomainEventRecord LegacyRecord(Uuid tenantId, Uuid programId, DomainEvent sourceEvent,
        ulong sourceVersion, DateTimeOffset sourceOccurredAt)
    {
        Assert.True(FindingHistoryEventPayloads.TryCreate(sourceEvent, tenantId, programId,
            FindingId(sourceEvent), out var payload));
        var sourceCopy = FindingHistoryEventPayloads.Deserialize(payload.Kind, payload.Json);
        var stored = DomainEventSeed.Attach(sourceCopy, programId, sourceVersion,
            occurredOn: sourceOccurredAt);
        return new DomainEventRecord(new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString()), stored,
            sourceVersion - 1, EventCursor.Start);
    }

    static Uuid FindingId(DomainEvent sourceEvent) => sourceEvent switch
    {
        FindingRaised ev => ev.FindingId,
        FindingRevised ev => ev.FindingId,
        _ => throw new InvalidOperationException("The test source event is unsupported."),
    };

    static DomainEvent WithMetadata(DomainEvent sourceEvent, DomainEventMetadata metadata)
    {
        var (tenantId, programId, findingId) = sourceEvent switch
        {
            FindingRaised ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingRevised ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            _ => throw new InvalidOperationException("The test source event is unsupported."),
        };
        Assert.True(FindingHistoryEventPayloads.TryCreate(sourceEvent, tenantId, programId,
            findingId, out var payload));
        var copy = FindingHistoryEventPayloads.Deserialize(payload.Kind, payload.Json);
        copy.AttachMetadata(metadata);
        return copy;
    }

    static FindingRaised Raised(Uuid tenantId, Uuid programId, Uuid findingId,
        Bdgrz.Compliance.Features.AccessControl.ActorReference actor, DateTimeOffset now,
        ulong streamVersion) => DomainEventSeed.Attach(new FindingRaised(tenantId, programId,
            findingId, 1, new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.",
            "medium", "All", Uuid.CreateVersion4(),
            DateOnly.FromDateTime(now.UtcDateTime).AddDays(10), [], actor, now),
            FindingAggregate.StreamId(programId, findingId), streamVersion, occurredOn: now);
}
