using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaLabelSignalIntakeTests
{
    [Fact]
    public async Task ShouldRescheduleSignalGivenContentIsTemporarilyUnavailable()
    {
        // Arrange
        var (source, fact, events) = await CreateSourceAsync();
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(source, 0, fact, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        content.Unavailable = true;
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(
            new ReconcileCriteriaLabelSignal(stored.Source, 0, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(bus.Forwarded);
    }
    [Fact]
    public async Task ShouldRetryExactRecordGivenUnknownCreateOutcome()
    {
        // Arrange
        var (source, fact, events) = await CreateSourceAsync();
        var records = new MemoryRecords { ThrowAfterFirstCommit = true };
        var intake = new CriteriaLabelSignalIntake(events, new MemoryContent(), records, TimeProvider.System);

        // Act
        var failure = await Record.ExceptionAsync(async () => await intake.AcceptAsync(source, 0, fact, CancellationToken.None));
        var retry = await intake.AcceptAsync(source, 0, fact, CancellationToken.None);

        // Assert
        Assert.IsType<TimeoutException>(failure);
        Assert.True(retry.IsSuccess);
        Assert.Single(records.Items);
        Assert.Equal(CriteriaLabelSignal.ProcessName, records.Items.Values.Single().Key.ProcessKind);
    }

    [Fact]
    public async Task ShouldRejectMismatchedRecordGivenSameSignalKey()
    {
        // Arrange
        var (source, fact, events) = await CreateSourceAsync();
        var records = new MemoryRecords();
        var intake = new CriteriaLabelSignalIntake(events, new MemoryContent(), records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(source, 0, fact, CancellationToken.None)).IsSuccess);
        var original = records.Items.Single();
        records.Items[original.Key] = original.Value with { Episode = "different" };

        // Act
        var result = await intake.AcceptAsync(source, 0, fact, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectSourceMismatchGivenTheRecordHasNotBeenCreated()
    {
        // Arrange
        var (source, fact, events) = await CreateSourceAsync();
        var records = new MemoryRecords();
        var intake = new CriteriaLabelSignalIntake(events, new MemoryContent(), records, TimeProvider.System);
        var forged = new CriteriaEditionDraftRequestCancelled(fact.EditionId, fact.OperationId, 1,
            Uuid.CreateVersion4(), fact.Actor, null!);
        forged.AttachMetadata(fact.Metadata);

        // Act
        var result = await intake.AcceptAsync(source, 0, forged, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(records.Items);
    }

    [Fact]
    public async Task ShouldPersistIndependentRecordsGivenTwoTerminalEvents()
    {
        // Arrange
        var (source, first, events) = await CreateSourceAsync();
        var second = new CriteriaEditionDraftRequestCancelled(first.EditionId, Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), first.Actor, null!);
        second.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), first.Metadata.AggregateId, 2, DateTimeOffset.UtcNow));
        await events.AppendAsync(source, 1, [second]);
        var records = new MemoryRecords();
        var intake = new CriteriaLabelSignalIntake(events, new MemoryContent(), records, TimeProvider.System);

        // Act
        var one = await intake.AcceptAsync(source, 0, first, CancellationToken.None);
        var two = await intake.AcceptAsync(source, 1, second, CancellationToken.None);

        // Assert
        Assert.True(one.IsSuccess);
        Assert.True(two.IsSuccess);
        Assert.Equal(2, records.Items.Count);
        Assert.All(records.Items.Values, record => Assert.Single(record.Targets));
    }

    [Fact]
    public async Task ShouldBuildTypedSweepReceiverGivenDurableSignal()
    {
        // Arrange
        var (source, fact, events) = await CreateSourceAsync();
        var records = new MemoryRecords();
        var intake = new CriteriaLabelSignalIntake(events, new MemoryContent(), records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(source, 0, fact, CancellationToken.None)).IsSuccess);
        var record = records.Items.Values.Single();
        var step = new ProcessStep(record, "reconcile", "criteria-label-signal", null, Uuid.CreateVersion4());

        // Act
        var receiver = await CriteriaLabelSignalProcess.Definition().CreateReceiver(step, null!, CancellationToken.None);

        // Assert
        var command = Assert.IsType<ReconcileCriteriaLabelSignal>(receiver);
        Assert.Equal(NativeFactReference.Of(source, fact), command.Source);
        Assert.Equal(0UL, command.Position);
    }

    [Fact]
    public async Task ShouldFenceExactRequestGivenCommittedCancellation()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "bdgrz");
        var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
        request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var terminal = new CriteriaEditionDraftRequestCancelled(edition.Id, Uuid.CreateVersion4(), 1, operation, actor, null!);
        terminal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 2, DateTimeOffset.UtcNow));
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [request, terminal]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(edition.Stream, 1, terminal, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var command = new ReconcileCriteriaLabelSignal(stored.Source, 1, stored.Intent);
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(command, RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fence = Assert.IsType<FenceCriteriaEditionLabelRequest>(Assert.Single(bus.Forwarded));
        Assert.Equal(request.Metadata.EventId, fence.EditionRequestEventId);
        Assert.Equal(terminal.Metadata.EventId, fence.TerminalEventId);
        Assert.Equal(operation, fence.OperationId);
    }

    [Fact]
    public async Task ShouldFenceInsteadOfActivatingGivenReservationArrivesAfterCancellation()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "bdgrz");
        var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
        request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var terminal = new CriteriaEditionDraftRequestCancelled(edition.Id, Uuid.CreateVersion4(), 1, operation, actor, null!);
        terminal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 2, DateTimeOffset.UtcNow));
        var claim = new EditionLabelClaim("edition_1");
        var reservation = new EditionLabelReserved("edition_1", edition.Id, operation, 1, request.Metadata.EventId, actor);
        reservation.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 1, DateTimeOffset.UtcNow));
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [request, terminal]);
        await events.AppendAsync(claim.Stream, 0, [reservation]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(claim.Stream, 0, reservation, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(
            new ReconcileCriteriaLabelSignal(stored.Source, 0, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fence = Assert.IsType<FenceCriteriaEditionLabelRequest>(Assert.Single(bus.Forwarded));
        Assert.Equal(terminal.Metadata.EventId, fence.TerminalEventId);
        Assert.Equal(request.Metadata.EventId, fence.EditionRequestEventId);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenRefusalHasOnlyAnUnverifiedEventId()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var refusal = new CriteriaInitialLabelRefused(edition.Id, Uuid.CreateVersion4(), "edition_1", "contested",
            Uuid.CreateVersion4());
        refusal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [refusal]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(edition.Stream, 0, refusal, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(
            new ReconcileCriteriaLabelSignal(stored.Source, 0, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(bus.Forwarded);
    }

    [Fact]
    public async Task ShouldFenceInsteadOfReservingGivenRequestIsReplayedAfterCancellation()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "bdgrz");
        var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
        request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var terminal = new CriteriaEditionDraftRequestCancelled(edition.Id, Uuid.CreateVersion4(), 1, operation, actor, null!);
        terminal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 2, DateTimeOffset.UtcNow));
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [request, terminal]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(edition.Stream, 0, request, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(
            new ReconcileCriteriaLabelSignal(stored.Source, 0, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var fence = Assert.IsType<FenceCriteriaEditionLabelRequest>(Assert.Single(bus.Forwarded));
        Assert.Equal(request.Metadata.EventId, fence.EditionRequestEventId);
        Assert.Equal(terminal.Metadata.EventId, fence.TerminalEventId);
    }

    [Fact]
    public async Task ShouldDeliverClaimRefusalGivenExactLiveHolderEvidence()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("operator", "bdgrz");
        var request = new CriteriaEditionDraftRequested(edition.Id, operation, operation, 1, "tsc", "edition_1", actor, null, null, null);
        request.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), edition.Id, 1, DateTimeOffset.UtcNow));
        var claim = new EditionLabelClaim("edition_1");
        var holder = new EditionLabelReserved("edition_1", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), actor);
        holder.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 1, DateTimeOffset.UtcNow));
        var refusal = new EditionLabelReservationRefused("edition_1", edition.Id, operation, 2, request.Metadata.EventId,
            actor, holder.EditionId, holder.OperationId, holder.Generation, holder.Metadata.EventId);
        refusal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), claim.Id, 2, DateTimeOffset.UtcNow));
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [request]);
        await events.AppendAsync(claim.Stream, 0, [holder, refusal]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaLabelSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(claim.Stream, 1, refusal, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaLabelSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaLabelSignal>(
            new ReconcileCriteriaLabelSignal(stored.Source, 1, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var apply = Assert.IsType<ApplyCriteriaEditionLabelRefusal>(Assert.Single(bus.Forwarded));
        Assert.Equal(refusal.Metadata.EventId, apply.SourceEventId);
        Assert.Equal(operation, apply.OperationId);
    }

    static async Task<(EventStreamAddress Stream, CriteriaEditionDraftRequestCancelled Fact, InMemoryEventStore Store)> CreateSourceAsync()
    {
        var owner = new CriteriaEdition(Uuid.CreateVersion4());
        var fact = new CriteriaEditionDraftRequestCancelled(owner.Id, Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), new ActorAttribution("operator", "bdgrz"), null!);
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UtcNow));
        var store = new InMemoryEventStore();
        await store.AppendAsync(owner.Stream, 0, [fact]);
        return (owner.Stream, fact, store);
    }

    sealed class MemoryRecords : IProcessRecordStore
    {
        public Dictionary<ProcessKey, ProcessRecord> Items { get; } = [];
        public bool ThrowAfterFirstCommit { get; set; }

        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
            Task.FromResult(Items.GetValueOrDefault(key));
        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
        {
            if (!Items.TryGetValue(record.Key, out var stored))
            {
                stored = record with { Version = 1 };
                Items.Add(record.Key, stored);
                if (ThrowAfterFirstCommit)
                {
                    ThrowAfterFirstCommit = false;
                    throw new TimeoutException("The create acknowledgement was lost.");
                }
            }
            return Task.FromResult(stored);
        }
        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class MemoryContent : IImmutableContentStore
    {
        readonly Dictionary<Uuid, byte[]> _items = [];
        public bool Unavailable { get; set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            _items[contentId] = bytes.ToArray();
            return Task.FromResult(new ImmutableContentRef(contentId, Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant(),
                bytes.Length, mediaType, schema));
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            Unavailable ? throw new ImmutableContentUnavailableException("The content is not yet visible.") :
                Task.FromResult<ReadOnlyMemory<byte>>(_items[reference.ContentId]);
    }
}
