using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Fixed-cut reads over <see cref="IStreamClient"/>. Fitz continuation tokens apply only to global selectors, so
/// every scan is global and the requested realm is verified per record before anything is deserialized.
/// </summary>
sealed class FitzAuthoritativeSnapshotReader(IStreamClient streams, IDomainEventSerializer serializer,
    TimeProvider? time = null) : IAuthoritativeSnapshotReader
{
    public const string AnchorRoute = "stream://**";
    const ulong PageSize = 256;

    public async Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default)
    {
        var page = await streams.ReadPageAsync(AnchorRoute, 0, 1, new StreamFilterSet(), null, null, null, ct)
            .ConfigureAwait(false);
        var watermark = page.Cursor.CapturedWatermark ?? throw new SourceFactVerificationException(
            "The broker did not report a global watermark.");
        return new SnapshotCapture(
            new AuthoritativeReadCut(AnchorRoute, watermark, (time ?? TimeProvider.System).GetUtcNow()),
            page.Cursor.CursorFingerprint);
    }

    public async Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope,
        AuthoritativeReadCut cut, SnapshotCursor? cursor = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantScope);
        ArgumentNullException.ThrowIfNull(cut);
        var route = selector.Route;
        if (cursor is not null && !string.Equals(cursor.Route, route, StringComparison.Ordinal))
            throw new SourceFactVerificationException("The cursor belongs to a different selector.");
        var watermark = cut.GlobalWatermarkExclusive;
        var state = cursor ?? new SnapshotCursor(route, 0, null, null);
        var facts = new List<ResolvedSourceFact>();
        var nextResource = new Dictionary<string, ulong>(StringComparer.Ordinal);
        var fresh = cursor is null;
        while (state.NextGlobalOffset < watermark)
        {
            ct.ThrowIfCancellationRequested();
            var page = await streams.ReadPageAsync(route, state.NextGlobalOffset, PageSize, new StreamFilterSet(),
                null, state.Fingerprint, state.CapturedWatermark, ct).ConfigureAwait(false);
            var snapshot = page.Cursor.CapturedWatermark ?? throw new SourceFactVerificationException(
                "The broker did not report a selector watermark.");
            if (snapshot < watermark)
                throw new SourceFactVerificationException("The selector snapshot is older than the cut.");
            if (state.CapturedWatermark is { } pinned && pinned != snapshot)
                throw new SourceFactVerificationException("The selector snapshot changed between pages.");
            var reached = false;
            var next = state.NextGlobalOffset;
            foreach (var item in page.Items)
            {
                if (item.Kind != StreamReadItemKind.Event || item.Record is not { } record)
                    throw new SourceFactVerificationException(
                        "A relevant record was withheld, so the source coverage is incomplete.");
                if (record.GlobalOffset is not { } globalOffset || record.RealmOffset is not { } realmOffset)
                    throw new SourceFactVerificationException("The broker did not report realm and global offsets.");
                if (globalOffset < next)
                    throw new SourceFactVerificationException("The broker returned records out of global order.");
                if (globalOffset >= watermark)
                {
                    reached = true;
                    break;
                }
                next = globalOffset + 1;
                CheckContinuity(nextResource, record, fresh);
                var address = Address(record.Route);
                if (!string.Equals(address.Realm, tenantScope, StringComparison.Ordinal))
                    continue;
                var domainEvent = Deserialize(record.Body);
                if (domainEvent.Metadata is { IsAudit: true })
                    continue;
                facts.Add(SourceFactVerifier.Build(address, record.Offset, realmOffset, globalOffset, domainEvent));
            }
            state = state with
            {
                Fingerprint = page.Cursor.CursorFingerprint ?? state.Fingerprint,
                CapturedWatermark = snapshot,
            };
            if (reached || !page.Cursor.HasMore)
                break;
            if (page.Cursor.LastGlobalOffset is { } last)
                next = Math.Max(next, last + 1);
            if (next <= state.NextGlobalOffset)
                throw new SourceFactVerificationException("The broker page made no progress.");
            state = state with { NextGlobalOffset = next };
        }
        // Coverage reached the cut. An empty complete scan checkpoints the watermark itself (zero), never offset 1.
        return new SnapshotReadResult(facts, state with { NextGlobalOffset = watermark }, true);
    }

    static void CheckContinuity(Dictionary<string, ulong> nextResource, StreamRecord record, bool fresh)
    {
        if (nextResource.TryGetValue(record.Route, out var expected))
        {
            if (record.Offset != expected)
                throw new SourceFactVerificationException(
                    $"A record is missing from {record.Route} before offset {record.Offset}.");
        }
        else if (fresh && record.Offset != 0)
        {
            throw new SourceFactVerificationException(
                $"The oldest records of {record.Route} are no longer retained.");
        }
        nextResource[record.Route] = record.Offset + 1;
    }

    static EventStreamAddress Address(string route)
    {
        try
        {
            return EventStreamAddress.Parse(route);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The record route cannot be parsed: {exception.Message}");
        }
    }

    DomainEvent Deserialize(ReadOnlyMemory<byte> body)
    {
        try
        {
            return serializer.Deserialize(body);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The source record cannot be deserialized: {exception.Message}");
        }
    }
}
