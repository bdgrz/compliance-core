using Cntryl.Fitz;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

sealed class ResolverStreamClient(Func<string, ulong, ulong?, ulong?, StreamReadPage> read) : IStreamClient
{
    public List<(string Route, ulong Start, ulong? Fingerprint, ulong? Watermark)> Requests { get; } = [];

    public Task<StreamReadPage> ReadPageAsync(string route, ulong startOffset, ulong limit = 100,
        StreamFilterSet? filter = null, ulong? maxBytes = null, ulong? cursorFingerprint = null,
        ulong? capturedWatermark = null, CancellationToken ct = default)
    {
        Requests.Add((route, startOffset, cursorFingerprint, capturedWatermark));
        return Task.FromResult(read(route, startOffset, cursorFingerprint, capturedWatermark));
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
}
