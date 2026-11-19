using Cntryl.Fitz;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using System.Runtime.CompilerServices;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

sealed class ResolverStreamClient(Func<string, ulong, IEnumerable<StreamReadBatch>> read) : IStreamClient
{
    public List<(string Route, ulong Start, StreamReadMode Mode, ulong BatchSize)> Requests { get; } = [];

    public async IAsyncEnumerable<StreamReadBatch> ReadAsync(string route, ulong startOffset,
        StreamReadMode mode, StreamReadOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new StreamReadOptions();
        Requests.Add((route, startOffset, mode, options.BatchSize));
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (var batch in read(route, startOffset))
        {
            ct.ThrowIfCancellationRequested();
            yield return batch;
        }
    }

    public Task<IStreamSession> BeginAsync(string route, ReadOnlyMemory<byte>? ingestMetadata = null,
        CancellationToken ct = default) => throw new NotSupportedException();
    public Task<StreamRecord?> PeekAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamMetadata> MetadataAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
