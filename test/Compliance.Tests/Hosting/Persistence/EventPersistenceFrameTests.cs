using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Hosting.Persistence;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class EventPersistenceFrameTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    static readonly EventStreamAddress Stream = new("ténant", "events", "récord");
    readonly Uuid _aggregate = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRejectBeforeSessionGivenEnvelopeCannotRoundTripExactly()
    {
        // Arrange
        using var provider = Compose();
        var serializer = new NonCanonicalSerializer(provider.GetRequiredService<IDomainEventSerializer>());
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.AppendAsync(Stream, 0, [Event("Acme", 1)]));

        // Assert
        Assert.Contains("canonical envelope", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, streams.Begins);
        Assert.Empty(streams.Bodies);
    }

    [Fact]
    public async Task ShouldPersistCheckedSnapshotGivenPayloadChangesWhileSessionOpens()
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var ids = new List<Uuid> { Uuid.CreateVersion4() };
        var streams = new RecordingStreamClient
        {
            Beginning = () => ids.AddRange(Enumerable.Range(0, 2000).Select(_ => Uuid.CreateVersion4())),
        };
        var store = Guard(new FitzEventStore(streams, serializer), serializer);
        var ev = new ResponsibilityAssigned(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            ResponsibilityType.ControlOwner, new ResponsibilityScope("control", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1), Now, Uuid.CreateVersion4(), Now, null, ids, "Test");
        ev.AttachMetadata(Event("metadata", 1).Metadata);
        var checkedBytes = serializer.Serialize(ev).ToArray();

        // Act
        await store.AppendAsync(Stream, 0, [ev]);

        // Assert
        Assert.Equal(2001, ids.Count);
        Assert.Equal(checkedBytes, Assert.Single(streams.Bodies));
        Assert.Single(Assert.IsType<ResponsibilityAssigned>(serializer.Deserialize(streams.Bodies[0])).SeparationOfDutiesWaiverIds);
    }

    [Fact]
    public async Task ShouldRejectWholeBatchBeforeSessionGivenOversizedLaterEvent()
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);
        var first = Event("Acme", 1);
        var second = Event(new string('x', 70_000), 2);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(Stream, 0, [first, second]));

        // Assert
        Assert.IsType<EventFrameSizeException>(failure);
        Assert.Equal(0, streams.Begins);
        Assert.Empty(streams.Bodies);
        Assert.Equal(0, streams.Commits);
    }

    [Fact]
    public async Task ShouldIncludeAttributionInLimitGivenSmallBusinessPayload()
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);
        var ev = Event("Acme", 1, new string('a', 66_000));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(Stream, 0, [ev]));

        // Assert
        Assert.IsType<EventFrameSizeException>(failure);
        Assert.Equal(0, streams.Begins);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShouldEnforceExactAppendBoundaryGivenUnicodeRoute(int excess)
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);
        var overhead = serializer.Serialize(Event("", 1)).Length;
        var allowed = ushort.MaxValue - Encoding.UTF8.GetByteCount(Stream.ToString()) - 26;
        var ev = Event(new string('x', allowed - overhead + excess), 1);
        Assert.Equal(allowed + excess, serializer.Serialize(ev).Length);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await store.AppendAsync(Stream, 7, [ev]));

        // Assert
        if (excess == 0)
        {
            Assert.Null(failure);
            Assert.Equal(1, streams.Begins);
            Assert.Equal(1, streams.Commits);
            Assert.Equal(7ul, Assert.Single(streams.Offsets));
            Assert.Equal(serializer.Serialize(ev).ToArray(), Assert.Single(streams.Bodies));
            Assert.Equal(Encoding.UTF8.GetBytes(Stream.ToString()), Assert.Single(streams.Metadata));
            var operation = PinnedFitzFrameTests.OperationPayload(true, streams.Metadata[0], [], streams.Bodies[0]);
            Assert.Equal(ushort.MaxValue + 16, PinnedFitzFrameTests.Encode("StreamAppend", operation).Length);
        }
        else
        {
            Assert.IsType<EventFrameSizeException>(failure);
            Assert.Equal(0, streams.Begins);
        }
    }

    [Fact]
    public async Task ShouldSerializeWholeBatchBeforeSessionGivenLaterSerializerFailure()
    {
        // Arrange
        using var provider = Compose();
        var serializer = new FailingSerializer(provider.GetRequiredService<IDomainEventSerializer>());
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);

        // Act
        var failure = await Assert.ThrowsAsync<JsonException>(async () =>
            await store.AppendAsync(Stream, 0, [Event("Acme", 1), Event("fail", 2)]));

        // Assert
        Assert.Equal("Serializer failure", failure.Message);
        Assert.Equal(0, streams.Begins);
        Assert.Empty(streams.Bodies);
    }

    [Fact]
    public async Task ShouldAvoidSessionGivenEmptyOrCanceledBatch()
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var streams = new RecordingStreamClient();
        var store = Guard(new FitzEventStore(streams, serializer), serializer);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        await store.AppendAsync(Stream, 0, []);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.AppendAsync(Stream, 0, [Event("Acme", 1)], canceled.Token));

        // Assert
        Assert.Equal(0, streams.Begins);
    }

    [Fact]
    public async Task ShouldForwardReadsAndNotificationsGivenDecoratedEventStore()
    {
        // Arrange
        using var provider = Compose();
        var serializer = provider.GetRequiredService<IDomainEventSerializer>();
        var inner = new ObservableStore();
        var store = Guard(inner, serializer);
        var pattern = EventStreamPattern.ForPattern("ténant", "events");
        using var cancellation = new CancellationTokenSource();

        // Act
        await foreach (var _ in store.ReadAsync(Stream, 9, cancellation.Token))
        {
        }
        await foreach (var _ in store.ReadAsync(pattern, new EventCursor("cursor"), cancellation.Token))
        {
        }
        var subscription = await ((IDomainEventNotifier)store).SubscribeAsync(pattern, cancellation.Token);
        await subscription.WaitAsync(cancellation.Token);
        await subscription.DisposeAsync();

        // Assert
        Assert.Equal(Stream, inner.ReadStream);
        Assert.Equal(9ul, inner.ReadOffset);
        Assert.Equal(pattern, inner.ReadPattern);
        Assert.Equal(new EventCursor("cursor"), inner.ReadCursor);
        Assert.Equal(pattern, inner.SubscriptionPattern);
        Assert.Equal(cancellation.Token, inner.Token);
        Assert.True(inner.Subscription.Waited);
        Assert.True(inner.Subscription.Disposed);
    }

    TenantRegistered Event(string name, ulong revision, string subject = "subject")
    {
        var ev = new TenantRegistered(_aggregate, Uuid.CreateVersion4(), name, "acme");
        ev.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), _aggregate, revision,
            Now, Uuid.CreateVersion4(), Uuid.CreateVersion4())
        {
            ExecutionId = Uuid.CreateVersion4(),
            Actor = new ActorAttribution(subject, "issuer"),
        });
        return ev;
    }

    internal static ServiceProvider Compose()
    {
        var services = new ServiceCollection();
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());
        return services.BuildServiceProvider();
    }

    static PersistenceEventStore Guard(IEventStore inner, IDomainEventSerializer serializer) =>
        new(inner, serializer);

    sealed class FailingSerializer(IDomainEventSerializer inner) : IDomainEventSerializer
    {
        public ReadOnlyMemory<byte> Serialize(DomainEvent ev) => ev is TenantRegistered { Name: "fail" }
            ? throw new JsonException("Serializer failure") : inner.Serialize(ev);
        public DomainEvent Deserialize(ReadOnlyMemory<byte> data) => inner.Deserialize(data);
    }

    sealed class NonCanonicalSerializer(IDomainEventSerializer inner) : IDomainEventSerializer
    {
        public ReadOnlyMemory<byte> Serialize(DomainEvent ev) => inner.Serialize(ev);
        public DomainEvent Deserialize(ReadOnlyMemory<byte> data)
        {
            var ev = (TenantRegistered)inner.Deserialize(data);
            var changed = ev with { Name = "Changed while snapshotting" };
            changed.AttachMetadata(ev.Metadata);
            return changed;
        }
    }

    sealed class RecordingStreamClient : IStreamClient, IStreamSession
    {
        public int Begins { get; private set; }
        public int Commits { get; private set; }
        public List<byte[]> Bodies { get; } = [];
        public List<byte[]> Metadata { get; } = [];
        public List<ulong> Offsets { get; } = [];
        public Action? Beginning { get; init; }

        public Task<IStreamSession> BeginAsync(string route, ReadOnlyMemory<byte>? ingestMetadata = null,
            CancellationToken ct = default)
        {
            Beginning?.Invoke();
            Begins++;
            return Task.FromResult<IStreamSession>(this);
        }

        public Task<ulong?> AppendAsync(ulong expectedOffset, ReadOnlyMemory<byte> body,
            ReadOnlyMemory<byte>? metadata = null, string? discriminator = null, CancellationToken ct = default)
        {
            Bodies.Add(body.ToArray());
            Metadata.Add(metadata?.ToArray() ?? []);
            Offsets.Add(expectedOffset);
            return Task.FromResult<ulong?>(expectedOffset);
        }

        public Task CommitAsync(StreamCommitMode mode, CancellationToken ct = default)
        {
            Assert.Equal(StreamCommitMode.Sync, mode);
            Commits++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public IAsyncEnumerable<StreamRecord> ReadAsync(string route, ulong startOffset, ulong limit = 100,
            StreamFilterSet? filter = null, ulong? maxBytes = null, ulong? cursorFingerprint = null,
            ulong? capturedWatermark = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StreamReadPage> ReadPageAsync(string route, ulong startOffset, ulong limit = 100,
            StreamFilterSet? filter = null, ulong? maxBytes = null, ulong? cursorFingerprint = null,
            ulong? capturedWatermark = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StreamRecord?> PeekAsync(string route, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StreamMetadata> MetadataAsync(string route, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StreamSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class ObservableStore : IEventStore, IDomainEventNotifier
    {
        public EventStreamAddress? ReadStream { get; private set; }
        public ulong ReadOffset { get; private set; }
        public EventStreamPattern? ReadPattern { get; private set; }
        public EventCursor? ReadCursor { get; private set; }
        public EventStreamPattern? SubscriptionPattern { get; private set; }
        public CancellationToken Token { get; private set; }
        public ObservableSubscription Subscription { get; } = new();
        public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            ReadStream = stream;
            ReadOffset = fromOffset;
            Token = ct;
            await Task.CompletedTask;
            yield break;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            ReadPattern = pattern;
            ReadCursor = cursor;
            Token = ct;
            await Task.CompletedTask;
            yield break;
        }
        public ValueTask<IDomainEventSubscription> SubscribeAsync(EventStreamPattern pattern, CancellationToken ct = default)
        {
            SubscriptionPattern = pattern;
            Token = ct;
            return ValueTask.FromResult<IDomainEventSubscription>(Subscription);
        }
    }

    sealed class ObservableSubscription : IDomainEventSubscription
    {
        public bool Waited { get; private set; }
        public bool Disposed { get; private set; }
        public ValueTask WaitAsync(CancellationToken ct = default)
        {
            Waited = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
