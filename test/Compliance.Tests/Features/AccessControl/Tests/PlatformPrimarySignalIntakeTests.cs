using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PlatformPrimarySignalIntakeTests
{
    [Fact]
    public async Task ShouldReuseAttemptGivenCreateAcknowledgementWasLost()
    {
        // Arrange
        var operation = Uuid.CreateVersion4();
        var seat = new PrimaryPlatformOperators(operation);
        var scenario = new AggregateScenario<PrimaryPlatformOperators>(seat);
        Assert.True(seat.ReserveInitial(operation, 0, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ActorAttribution("portia:system", "Portia")).IsSuccess);
        var reservation = Assert.IsType<PrimaryPlatformOperatorSeatReserved>(Assert.Single(scenario.PendingEvents));
        var source = new Reader(reservation);
        var store = new Records { LoseFirst = true };
        var intake = new PlatformPrimarySignalIntake(source, source, new Content(), store, TimeProvider.System);

        // Act
        var lost = await Record.ExceptionAsync(async () => await intake.AcceptAsync(seat.Stream, 0, reservation, CancellationToken.None));
        var retried = await intake.AcceptAsync(seat.Stream, 0, reservation, CancellationToken.None);

        // Assert
        Assert.IsType<TimeoutException>(lost);
        Assert.True(retried.IsSuccess);
        Assert.Equal(1, source.Hydrations);
        var record = Assert.Single(store.Items.Values);
        Assert.Equal("0:0:0", Assert.Single(record.Targets));
        Assert.Equal(reservation.Metadata.EventId, record.Source.EventId);
    }

    sealed class Reader(DomainEvent source) : IDomainEventReader, IAggregateReader
    {
        public int Hydrations { get; private set; }
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            Hydrations++;
            return ValueTask.FromResult(aggregate);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromVersion,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (stream == PrimaryPlatformOperators.Address && fromVersion == 0)
                yield return new DomainEventRecord(stream, source, 0, EventCursor.Start);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor after,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class Records : IProcessRecordStore
    {
        public Dictionary<ProcessKey, ProcessRecord> Items { get; } = [];
        public bool LoseFirst { get; set; }
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(key));
        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
        {
            if (!Items.TryGetValue(record.Key, out var stored))
                Items[record.Key] = stored = record with { Version = 1 };
            if (LoseFirst)
            { LoseFirst = false; throw new TimeoutException(); }
            return Task.FromResult(stored);
        }
        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit, CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class Content : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => Task.FromResult(new ImmutableContentRef(contentId,
                Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant(), bytes.Length, mediaType, schema));
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
