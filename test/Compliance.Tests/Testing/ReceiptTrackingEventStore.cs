using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit test commit authority: records its controlled clock only after a successful append, independently of event/operation timestamps.</summary>
sealed class ReceiptTrackingEventStore(IEventStore inner, TimeProvider clock) : IEventStore, INativeEventReceiptReader
{
    readonly object _gate = new();
    readonly Dictionary<(EventStreamAddress Stream, ulong Offset), (Uuid Event, ulong Version, string Kind, string Sha256, DateTimeOffset At)> _receipts = [];
    public TimeProvider Clock { get; set; } = clock;
    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct) => inner.ReadAsync(stream, offset, ct);
    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);
    public async ValueTask AppendAsync(EventStreamAddress stream, ulong expected, IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        await inner.AppendAsync(stream, expected, events, ct).ConfigureAwait(false);
        var at = DateTimeOffset.FromUnixTimeMilliseconds(Clock.GetUtcNow().ToUnixTimeMilliseconds());
        lock (_gate)
            for (var index = 0; index < events.Count; index++)
            {
                var value = events[index];
                _receipts.Add((stream, checked(expected + (ulong)index)), (value.Metadata.EventId, value.Metadata.AggregateVersion,
                    EventKind.Of(value), ContentDigest.Sha256(value, value.GetType()), at));
            }
    }
    public ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_receipts.TryGetValue((source.Stream, source.ResourceOffset), out var value))
                throw new ImmutableContentUnavailableException("The test commit receipt is absent.");
            if (source.Event.Metadata.IsAudit || source.Event.Metadata.EventId != value.Event || source.Event.Metadata.AggregateVersion != value.Version ||
                EventKind.Of(source.Event) != value.Kind || ContentDigest.Sha256(source.Event, source.Event.GetType()) != value.Sha256)
                throw new ImmutableContentIntegrityException("The test source differs from its exact recorded commit receipt.");
            return ValueTask.FromResult(value.At);
        }
    }
}
