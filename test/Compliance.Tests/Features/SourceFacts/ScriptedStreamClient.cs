using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

/// <summary>
/// Scripted Fitz stream client that mimics the broker's global continuation rules: each request without a token
/// takes a fresh snapshot, each selector owns a fingerprint, and a token on a realm selector is rejected.
/// </summary>
sealed class ScriptedStreamClient : IStreamClient
{
    readonly List<Stored> _records = [];

    public int PageSize { get; init; } = 100;
    public List<Request> Requests { get; } = [];
    public int Total => _records.Count;

    public void Append(EventStreamAddress stream, ReadOnlyMemory<byte> body, bool filtered = false, ulong? skipResource = null)
    {
        var route = stream.ToString();
        var resource = skipResource ?? (ulong)_records.Count(record => record.Route == route);
        var realm = (ulong)_records.Count(record => record.Address.Realm == stream.Realm);
        _records.Add(new Stored(route, stream, resource, realm, (ulong)_records.Count, body.ToArray(), filtered));
    }

    public static ulong FingerprintOf(string route, ulong snapshot) =>
        (ulong)StringComparer.Ordinal.GetHashCode(route) * 31UL + snapshot + 1;

    public Task<StreamReadPage> ReadPageAsync(string route, ulong startOffset, ulong limit = 100,
        StreamFilterSet? filter = null, ulong? maxBytes = null, ulong? cursorFingerprint = null,
        ulong? capturedWatermark = null, CancellationToken ct = default)
    {
        Requests.Add(new Request(route, startOffset, cursorFingerprint, capturedWatermark));
        if (!route.StartsWith("stream://", StringComparison.Ordinal))
            throw new InvalidOperationException("A stream selector must use its canonical scheme.");
        var global = route == "stream://**" || route.StartsWith("stream://*/", StringComparison.Ordinal);
        if (!global && (cursorFingerprint is not null || capturedWatermark is not null))
            throw new InvalidOperationException("Continuation tokens apply only to global selectors.");
        var snapshot = capturedWatermark ?? (ulong)_records.Count;
        if (cursorFingerprint is { } supplied && supplied != FingerprintOf(route, snapshot))
            throw new InvalidOperationException("The cursor fingerprint does not match this selector.");
        var matching = _records.Where(record => record.GlobalOffset >= startOffset && record.GlobalOffset < snapshot &&
            Matches(route, record)).ToList();
        var take = matching.Take((int)Math.Min(limit, (ulong)PageSize)).ToList();
        var items = take.Select(record => record.Filtered
            ? new StreamReadItem(record.Route, StreamReadItemKind.Filtered, null, record.ResourceOffset, 0, 0,
                StreamFilteredReason.Permission)
            : new StreamReadItem(record.Route, StreamReadItemKind.Event,
                new StreamRecord(record.Route, record.ResourceOffset, null, record.RealmOffset, record.GlobalOffset,
                    record.Body, null, 0), 0, 0, 0, null)).ToList();
        var cursor = new StreamReadCursor(take.Count == 0 ? 0 : take[^1].ResourceOffset, null, null,
            take.Count == 0 ? null : take[^1].GlobalOffset, FingerprintOf(route, snapshot), snapshot,
            matching.Count > take.Count);
        return Task.FromResult(new StreamReadPage(items, cursor));
    }

    static bool Matches(string route, Stored record)
    {
        if (route == "stream://**")
            return true;
        var parts = route["stream://".Length..].Split('/');
        return parts[1] == record.Address.Area && (parts[2] == "*" || parts[2] == record.Address.Resource);
    }

    public Task<IStreamSession> BeginAsync(string route, ReadOnlyMemory<byte>? ingestMetadata = null,
        CancellationToken ct = default) => throw new NotSupportedException();
    public IAsyncEnumerable<StreamRecord> ReadAsync(string route, ulong startOffset, ulong limit = 100,
        StreamFilterSet? filter = null, ulong? maxBytes = null, ulong? cursorFingerprint = null,
        ulong? capturedWatermark = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<StreamRecord?> PeekAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamMetadata> MetadataAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public sealed record Request(string Route, ulong Start, ulong? Fingerprint, ulong? Watermark);

    sealed record Stored(string Route, EventStreamAddress Address, ulong ResourceOffset, ulong RealmOffset,
        ulong GlobalOffset, byte[] Body, bool Filtered);
}
