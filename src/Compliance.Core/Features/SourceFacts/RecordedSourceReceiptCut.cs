using Bdgrz.Compliance.Features.SourceFacts.Readers;
using System.Collections.Concurrent;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Verified inclusive broker-millisecond prefix selection; payload occurrence/preparation time never supplies receipt authority.</summary>
public sealed class RecordedSourceReceiptCut(Uuid tenant, DateTimeOffset through, IDomainEventReader events, INativeEventReceiptReader receipts)
{
    readonly ConcurrentDictionary<EventStreamAddress, Lazy<Task<RecordedSourceReceiptPrefix>>> _streams = new();
    public Uuid TenantId { get; } = tenant;
    public DateTimeOffset RecordedThrough { get; } = through;
    public Task<RecordedSourceReceiptPrefix> ReadAsync(EventStreamAddress stream, CancellationToken ct = default)
    {
        if (stream.Realm != TenantId.ToString())
            throw new ImmutableContentIntegrityException("The receipt selection belongs to another exact tenant scope.");
        return _streams.GetOrAdd(stream, key => new(() => CaptureAsync(key, ct))).Value;
    }
    public async ValueTask<bool> IncludesAsync(DomainEventRecord source, CancellationToken ct = default) =>
        (await ReadAsync(source.Stream, ct).ConfigureAwait(false)).Includes(source.Event);
    public async ValueTask<bool> IncludesAsync(EventStreamAddress stream, DomainEvent source, CancellationToken ct = default) =>
        (await ReadAsync(stream, ct).ConfigureAwait(false)).Includes(source);
    async Task<RecordedSourceReceiptPrefix> CaptureAsync(EventStreamAddress stream, CancellationToken ct)
    {
        var all = new List<DomainEventRecord>();
        var included = new HashSet<Uuid>();
        var excluded = false;
        var position = 0UL;
        var ids = new HashSet<Uuid>();
        var recordedAt = new Dictionary<Uuid, DateTimeOffset>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.ResourceOffset != position++)
                throw new ImmutableContentIntegrityException("The receipt source stream has a foreign record or physical gap.");
            var metadata = record.Event.Metadata;
            if (metadata.IsAudit)
                continue;
            if (metadata.EventId == Uuid.Empty || metadata.AggregateVersion == 0 || !ids.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The receipt source stream lacks unique committed native identity.");
            var at = await receipts.RecordedAtAsync(record, ct).ConfigureAwait(false);
            if (at <= DateTimeOffset.UnixEpoch || at != DateTimeOffset.FromUnixTimeMilliseconds(at.ToUnixTimeMilliseconds()))
                throw new ImmutableContentIntegrityException("The source receipt is not a valid broker epoch-millisecond timestamp.");
            all.Add(record);
            recordedAt.Add(metadata.EventId, at);
            if (at > RecordedThrough)
            { excluded = true; continue; }
            if (excluded)
                throw new ImmutableContentIntegrityException("The broker receipt cutoff does not select a complete causal prefix of its source stream.");
            included.Add(metadata.EventId);
        }
        return new(stream, all.AsReadOnly(), included, recordedAt);
    }
}
