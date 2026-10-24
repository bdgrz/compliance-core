using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     An in-memory event store that runs one action right after a chosen area scan completes, so a test can append
///     a newer fact between a reactor's source scan and the commands it sends from that scan.
/// </summary>
sealed class ScanHookEventStore : IEventStore
{
    readonly InMemoryEventStore _inner = new();
    EventStreamPattern? _pattern;
    Func<Task>? _afterScan;

    /// <summary>Runs <paramref name="action" /> once, after the next complete scan of <paramref name="pattern" />.</summary>
    public void AfterNextScan(EventStreamPattern pattern, Func<Task> action)
    {
        _pattern = pattern;
        _afterScan = action;
    }

    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset,
        CancellationToken ct) => _inner.ReadAsync(stream, offset, ct);

    public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var record in _inner.ReadAsync(pattern, cursor, ct))
            yield return record;
        if (_afterScan is { } action && pattern == _pattern)
        {
            _afterScan = null;
            await action();
        }
    }

    public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
        IReadOnlyList<DomainEvent> events, CancellationToken ct = default) =>
        _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
}
