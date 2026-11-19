using System.Runtime.CompilerServices;
using System.Text;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>
/// Reads one concrete Fitz resource and preserves broker-reported area and optional realm
/// positions. It never reads an edition-wide pattern.
/// </summary>
/// <remarks>
/// An observed event proves only that event. Reaching the end of a read is not proof
/// that the source was current or that an unobserved event cannot exist. Callers need
/// a separately qualified fresh-to-head contract before using absence or latest state.
/// Realm positions are observations only; they do not establish cross-owner causality.
/// </remarks>
public sealed class FitzCriteriaExactSourceReader(IStreamClient streams, IDomainEventSerializer serializer)
    : ICriteriaExactSourceReader
{
    const ulong PageSize = 128;
    const ulong PageByteBudget = 256 * 1024;

    /// <summary>Finds one exact source event, or returns null if it was not observed.</summary>
    /// <remarks>An unobserved event is unknown, not a definitive absence or refusal.</remarks>
    public async ValueTask<CriteriaOrderedSourceEvent?> FindAsync(EventStreamAddress stream, Uuid aggregateId,
        Uuid eventId, CancellationToken ct = default)
    {
        if (eventId == Uuid.Empty)
            throw new ArgumentException("The exact event identity is required.", nameof(eventId));
        await foreach (var item in ReadAsync(stream, aggregateId, ct).ConfigureAwait(false))
        {
            if (item.Event.Metadata.EventId == eventId)
                return item;
        }

        return null;
    }

    /// <summary>Streams contiguous records from one concrete resource in bounded pages.</summary>
    /// <param name="stream">The exact source address.</param>
    /// <param name="aggregateId">The expected source aggregate identity.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>Committed events carrying broker-reported common-area positions.</returns>
    /// <exception cref="CriteriaSourceUnavailableException">The broker omitted required source evidence.</exception>
    /// <exception cref="InvalidOperationException">The source route, position or native event is inconsistent.</exception>
    public async IAsyncEnumerable<CriteriaOrderedSourceEvent> ReadAsync(EventStreamAddress stream, Uuid aggregateId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (aggregateId == Uuid.Empty)
            throw new ArgumentException("The source aggregate identity is required.", nameof(aggregateId));

        var route = stream.ToString();
        var routeBytes = Encoding.UTF8.GetBytes(route);
        ulong next = 0;
        ulong? lastArea = null;
        ulong? lastRealm = null;
        ulong? fingerprint = null;
        ulong? watermark = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await streams.ReadPageAsync(route, next, PageSize, new StreamFilterSet(),
                PageByteBudget, fingerprint, watermark, ct).ConfigureAwait(false);
            if (watermark is { } captured && page.Cursor.CapturedWatermark != captured)
                throw new CriteriaSourceUnavailableException("The source changed the captured read watermark.");
            var first = next;
            foreach (var item in page.Items)
            {
                if (item.Kind != StreamReadItemKind.Event || item.Record is not { } record)
                    throw new CriteriaSourceUnavailableException("The source withheld a criteria event.");
                if (!string.Equals(item.Route, route, StringComparison.Ordinal) ||
                    !string.Equals(record.Route, route, StringComparison.Ordinal) || record.Offset != next ||
                    record.Metadata is not { } metadata || !metadata.Span.SequenceEqual(routeBytes))
                    throw new InvalidOperationException("The exact criteria source route or position is inconsistent.");
                if (record.AreaOffset is not { } area)
                    throw new CriteriaSourceUnavailableException("The broker did not report an area position.");
                if (lastArea is { } previous && area <= previous)
                    throw new InvalidOperationException("The criteria source area positions are not ordered.");
                if (record.RealmOffset is { } realm && lastRealm is { } previousRealm && realm <= previousRealm)
                    throw new InvalidOperationException("The criteria source realm positions are not ordered.");

                var domainEvent = serializer.Deserialize(record.Body);
                var envelope = domainEvent.Metadata;
                if (envelope.EventId == Uuid.Empty || envelope.AggregateId != aggregateId || envelope.IsAudit ||
                    envelope.AggregateVersion != checked(record.Offset + 1))
                    throw new InvalidOperationException("The criteria event envelope does not match its owner.");
                yield return new CriteriaOrderedSourceEvent(stream, record.Offset, area, domainEvent)
                {
                    RealmOffset = record.RealmOffset,
                };
                lastArea = area;
                if (record.RealmOffset is { } observedRealm)
                    lastRealm = observedRealm;
                next = checked(next + 1);
            }

            if (!page.Cursor.HasMore)
                yield break;
            if (next == first || page.Cursor.LastResourceOffset != next - 1)
                throw new CriteriaSourceUnavailableException("The source cannot make contiguous progress.");
            fingerprint = page.Cursor.CursorFingerprint ?? fingerprint;
            watermark = page.Cursor.CapturedWatermark ?? watermark;
        }
    }
}
