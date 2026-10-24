using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Fixed-cut reader over an in-memory event store that reports one frontier above every record it holds. It only
///     serves concrete resource selectors, because the store reads one stream at a time; the broker-shaped scans are
///     covered by the scripted stream client tests of the real reader.
/// </summary>
sealed class EventStoreSnapshotReader(IDomainEventReader events) : IAuthoritativeSnapshotReader
{
    public const ulong Frontier = EventStoreSourceFactResolver.GlobalBase * 10;

    public Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default) => Task.FromResult(new SnapshotCapture(
        new AuthoritativeReadCut("stream://**", Frontier, DateTimeOffset.UnixEpoch), 1));

    public async Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope,
        AuthoritativeReadCut cut, SnapshotCursor? cursor = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var resource = selector.Resource ?? throw new SourceFactVerificationException(
            "The in-memory reader serves concrete resources only.");
        var stream = new EventStreamAddress(tenantScope, selector.Area, resource);
        var facts = new List<ResolvedSourceFact>();
        try
        {
            await foreach (var record in events.ReadAsync(stream, 0, ct))
            {
                if (record.Event.Metadata is { IsAudit: true })
                    continue;
                var global = EventStoreSourceFactResolver.GlobalBase + record.ResourceOffset;
                if (global >= cut.GlobalWatermarkExclusive)
                    break;
                facts.Add(SourceFactVerifier.Build(stream, record.ResourceOffset,
                    EventStoreSourceFactResolver.RealmBase + record.ResourceOffset, global, record.Event));
            }
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unknown stream this way: nothing is recorded below the cut.
        }

        return new SnapshotReadResult(facts, new SnapshotCursor(selector.Route, cut.GlobalWatermarkExclusive, null,
            cut.GlobalWatermarkExclusive), true);
    }
}
