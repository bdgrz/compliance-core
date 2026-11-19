using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaPublicationSignalTests
{
    [Fact]
    public async Task ShouldRescheduleSignalGivenContentIsTemporarilyUnavailable()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var started = Started(edition.Id);
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [started]);
        var records = new MemoryRecords();
        var content = new MemoryContent();
        var intake = new CriteriaPublicationSignalIntake(events, content, records, TimeProvider.System);
        Assert.True((await intake.AcceptAsync(edition.Stream, 0, started, CancellationToken.None)).IsSuccess);
        var stored = records.Items.Values.Single();
        var bus = new CapturingBus();
        var handler = new ReconcileCriteriaPublicationSignalHandler(events, content, bus);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ReconcileCriteriaPublicationSignal>(
            new ReconcileCriteriaPublicationSignal(stored.Source, 0, stored.Intent), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(bus.Forwarded);
    }
    [Fact]
    public async Task ShouldRetryWithoutDuplicateGivenUnknownSignalCreate()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var start = Started(edition.Id);
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [start]);
        var records = new MemoryRecords { LoseFirstAcknowledgement = true };
        var intake = new CriteriaPublicationSignalIntake(events, new MemoryContent(), records, TimeProvider.System);

        // Act
        var lost = await Record.ExceptionAsync(async () => await intake.AcceptAsync(edition.Stream, 0, start, CancellationToken.None));
        var replay = await intake.AcceptAsync(edition.Stream, 0, start, CancellationToken.None);

        // Assert
        Assert.IsType<TimeoutException>(lost);
        Assert.True(replay.IsSuccess);
        Assert.Single(records.Items);
    }

    [Fact]
    public async Task ShouldKeepSignalsIndependentGivenARefusalAndAnotherAttempt()
    {
        // Arrange
        var edition = new CriteriaEdition(Uuid.CreateVersion4());
        var first = Started(edition.Id);
        var witness = new CriteriaEntry(edition.Id, first.WitnessIdentifier);
        var refusal = new CriterionWitnessRefused(edition.Id, first.DraftGeneration, first.WitnessIdentifier,
            first.PublicationAttemptId, first.AttemptOrdinal, first.WitnessIncarnationId, first.Metadata.EventId);
        refusal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), witness.Id, 1, DateTimeOffset.UtcNow));
        var other = new CriteriaEdition(Uuid.CreateVersion4());
        var second = Started(other.Id);
        var events = new InMemoryEventStore();
        await events.AppendAsync(edition.Stream, 0, [first]);
        await events.AppendAsync(witness.Stream, 0, [refusal]);
        await events.AppendAsync(other.Stream, 0, [second]);
        var records = new MemoryRecords();
        var intake = new CriteriaPublicationSignalIntake(events, new MemoryContent(), records, TimeProvider.System);

        // Act
        var one = await intake.AcceptAsync(witness.Stream, 0, refusal, CancellationToken.None);
        var two = await intake.AcceptAsync(other.Stream, 0, second, CancellationToken.None);

        // Assert
        Assert.True(one.IsSuccess);
        Assert.True(two.IsSuccess);
        Assert.Equal(2, records.Items.Count);
        Assert.All(records.Items.Values, record => Assert.Single(record.Targets));
    }

    static CriteriaPublicationStarted Started(Uuid editionId)
    {
        var fact = new CriteriaPublicationStarted(editionId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, "CC6.1",
            Uuid.CreateVersion4(), "edition_1", 1, Uuid.CreateVersion4(), new ActorAttribution("operator", "bdgrz"), null);
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), editionId, 1, DateTimeOffset.UtcNow));
        return fact;
    }

    sealed class MemoryRecords : IProcessRecordStore
    {
        public Dictionary<ProcessKey, ProcessRecord> Items { get; } = [];
        public bool LoseFirstAcknowledgement { get; set; }
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(key));
        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
        {
            if (!Items.TryGetValue(record.Key, out var stored))
            {
                stored = record with { Version = 1 };
                Items.Add(record.Key, stored);
                if (LoseFirstAcknowledgement)
                {
                    LoseFirstAcknowledgement = false;
                    throw new TimeoutException("The record committed but the response was lost.");
                }
            }
            return Task.FromResult(stored);
        }
        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class MemoryContent : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => Task.FromResult(new ImmutableContentRef(contentId,
                Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant(), bytes.Length, mediaType, schema));
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw new ImmutableContentUnavailableException("The content is not yet visible.");
    }
}
