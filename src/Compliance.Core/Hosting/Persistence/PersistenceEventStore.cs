using Cntryl.Portia;

namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>Preflights every attributed envelope before the inner store can open an append session.</summary>
sealed class PersistenceEventStore(IEventStore inner, IDomainEventSerializer serializer)
    : IEventStore, IDomainEventNotifier
{
    public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
        IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(events);
        ct.ThrowIfCancellationRequested();
        DomainEvent[] pending = [.. events];
        var snapshots = new DomainEvent[pending.Length];
        for (var index = 0; index < pending.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var envelope = serializer.Serialize(pending[index]);
            FitzFrameLimits.CheckEvent(stream, envelope.Length);
            var snapshot = serializer.Deserialize(envelope);
            if (!serializer.Serialize(snapshot).Span.SequenceEqual(envelope.Span))
                throw new InvalidOperationException("A persisted event must preserve its canonical envelope through serialization.");
            snapshots[index] = snapshot;
        }
        return inner.AppendAsync(stream, expectedStreamPosition, snapshots, ct);
    }

    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
        CancellationToken ct) => inner.ReadAsync(stream, fromOffset, ct);

    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
        CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);

    public ValueTask<IDomainEventSubscription> SubscribeAsync(EventStreamPattern pattern,
        CancellationToken ct = default) => inner is IDomainEventNotifier notifier
        ? notifier.SubscribeAsync(pattern, ct)
        : throw new NotSupportedException("The underlying event store does not support notifications.");
}
