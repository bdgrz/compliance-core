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
        foreach (var ev in events)
        {
            ct.ThrowIfCancellationRequested();
            FitzFrameLimits.CheckEvent(stream, serializer.Serialize(ev).Length);
        }
        return inner.AppendAsync(stream, expectedStreamPosition, events, ct);
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
