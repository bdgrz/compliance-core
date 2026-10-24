using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
/// Process records on <c>kv://bdgrz/processes/{processKind}-{shard}</c> (shard = first two hex characters of the
/// root operation id, 256 per kind). Keys: <c>{realm}/p/{root}/{source}</c> holds the record;
/// <c>due/{unixMs:D20}/{realm}/{root}/{source}</c>
/// indexes non-terminal records by due time and is maintained in the same transaction. The route lock at BEGIN
/// makes read-compare-write the CAS; a lost lock is retried from BEGIN.
/// Payloads use verified 48 KiB chunks under a manifest, with a 16 MiB transaction budget. Payload, manifest and
/// due-index changes commit together; a large record is never truncated to fit one frame.
/// </summary>
sealed class FitzKvProcessRecordStore(IKvClient client) : IProcessRecordStore
{
    internal const int Attempts = 4;
    internal const int ChunkBytes = 48 * 1024;
    internal const int MaximumRecordBytes = 16 * 1024 * 1024;
    readonly ConcurrentDictionary<string, int> _nextShard = new(StringComparer.Ordinal);

    public async Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var tx = await client.BeginAsync(Route(key), KvMode.ReadOnly, ct).ConfigureAwait(false);
        return (await ReadStoredAsync(tx, key, ct).ConfigureAwait(false))?.Value;
    }

    public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(record.Key), KvMode.ReadWrite, ct).ConfigureAwait(false);
            var found = await ReadStoredAsync(tx, record.Key, ct).ConfigureAwait(false);
            if (found is not null)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return found.Value;
            }

            var stored = record with { Version = 1 };
            await WriteStoredAsync(tx, stored, 0, create: true, ct).ConfigureAwait(false);
            if (!stored.IsTerminal)
                await tx.InsertAsync(DueKey(stored), ReadOnlyMemory<byte>.Empty, ct).ConfigureAwait(false);
            await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
            return stored;
        });
    }

    public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(record.Key), KvMode.ReadWrite, ct).ConfigureAwait(false);
            var found = await ReadStoredAsync(tx, record.Key, ct).ConfigureAwait(false);
            if (found is null)
                throw new ProcessConcurrencyException($"Process {record.Key} no longer exists.");
            var current = found.Value;
            if (current.Version != expectedVersion)
                throw new ProcessConcurrencyException(
                    $"Process {record.Key} is at version {current.Version}, not {expectedVersion}.");
            var stored = record with { Version = expectedVersion + 1 };
            await WriteStoredAsync(tx, stored, found.Chunks, create: false, ct).ConfigureAwait(false);
            if (!current.IsTerminal)
                await tx.DeleteAsync(DueKey(current), ct).ConfigureAwait(false);
            if (!stored.IsTerminal)
                await tx.PutAsync(DueKey(stored), ReadOnlyMemory<byte>.Empty, ct).ConfigureAwait(false);
            await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
            return stored;
        });
    }

    public async Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore,
        int limit, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processKind);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var keys = new List<ProcessKey>();
        var end = Encoding.UTF8.GetBytes($"due/{Stamp(dueBefore.AddMilliseconds(1))}");
        var start = _nextShard.GetOrAdd(processKind, 0);
        var next = start;
        for (var offset = 0; offset < 256 && keys.Count < limit; offset++)
        {
            var shard = (start + offset) % 256;
            next = (shard + 1) % 256;
            await using var tx = await client.BeginAsync(Route(processKind, shard.ToString("x2", CultureInfo.InvariantCulture)),
                KvMode.ReadOnly, ct).ConfigureAwait(false);
            var page = await tx.ScanAsync(new KvScanQuery(Encoding.UTF8.GetBytes("due/"), end,
                (uint)(limit - keys.Count), false, false), ct).ConfigureAwait(false);
            foreach (var pair in page.Pairs)
            {
                var parts = Encoding.UTF8.GetString(pair.Key.Span).Split('/');
                if (parts.Length != 5)
                    continue;
                keys.Add(new ProcessKey(processKind, parts[2], Uuid.Parse(parts[3], CultureInfo.InvariantCulture),
                    Uuid.Parse(parts[4], CultureInfo.InvariantCulture)));
            }
        }
        _nextShard[processKind] = next;
        return keys;
    }

    static async Task<ProcessRecord> RetryAsync(Func<Task<ProcessRecord>> attempt)
    {
        for (var count = 1; ; count++)
        {
            try
            {
                return await attempt().ConfigureAwait(false);
            }
            catch (KvException exception) when (count < Attempts && IsIsolationConflict(exception))
            {
                // Lost the route lock to another writer of this shard; the transaction had no effect.
            }
        }
    }

    internal static bool IsIsolationConflict(KvException exception) =>
        exception.DomainCode == FitzErrorCodes.KvIsolationConflict ||
        string.Equals(exception.Code, nameof(FitzErrorCodes.KvIsolationConflict), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(exception.Code, "KV_ISOLATION_CONFLICT", StringComparison.OrdinalIgnoreCase);

    internal static string Route(ProcessKey key) =>
        Route(key.ProcessKind, key.RootOperationId.ToString("N", CultureInfo.InvariantCulture)[..2]);

    static string Route(string processKind, string shard) => $"kv://bdgrz/processes/{processKind}-{shard}";

    static string RecordPath(ProcessKey key) =>
        $"{key.Realm}/p/{key.RootOperationId.ToString("N", CultureInfo.InvariantCulture)}/{key.SourceId.ToString("N", CultureInfo.InvariantCulture)}";

    static byte[] RecordKey(ProcessKey key) => Encoding.UTF8.GetBytes(RecordPath(key));

    static byte[] ChunkKey(ProcessKey key, int index) =>
        Encoding.UTF8.GetBytes($"{RecordPath(key)}/chunk/{index.ToString("D6", CultureInfo.InvariantCulture)}");

    static byte[] DueKey(ProcessRecord record) => Encoding.UTF8.GetBytes(
        $"due/{Stamp(record.NextDueAt)}/{record.Key.Realm}/{record.Key.RootOperationId.ToString("N", CultureInfo.InvariantCulture)}/{record.Key.SourceId.ToString("N", CultureInfo.InvariantCulture)}");

    static string Stamp(DateTimeOffset at) =>
        Math.Max(0, at.ToUnixTimeMilliseconds()).ToString("D20", CultureInfo.InvariantCulture);

    static async Task<StoredRecord?> ReadStoredAsync(IKvTransaction tx, ProcessKey key, CancellationToken ct)
    {
        var found = await tx.GetAsync(RecordKey(key), ct).ConfigureAwait(false);
        if (!found.Found)
            return null;
        var manifest = JsonSerializer.Deserialize(found.Value!.Value.Span,
            ComplianceCoreJsonContext.Default.ProcessRecordManifest)
            ?? throw new InvalidOperationException("The process manifest is empty.");
        if (manifest.Bytes is < 1 or > MaximumRecordBytes ||
            manifest.Chunks != (manifest.Bytes + ChunkBytes - 1) / ChunkBytes)
            throw new InvalidOperationException("The process manifest has invalid byte or chunk counts.");
        var bytes = new byte[manifest.Bytes];
        var written = 0;
        for (var index = 0; index < manifest.Chunks; index++)
        {
            var chunk = await tx.GetAsync(ChunkKey(key, index), ct).ConfigureAwait(false);
            var expected = Math.Min(ChunkBytes, bytes.Length - written);
            if (!chunk.Found || chunk.Value!.Value.Length != expected)
                throw new InvalidOperationException("A process payload chunk is missing or incomplete.");
            chunk.Value.Value.Span.CopyTo(bytes.AsSpan(written));
            written += expected;
        }
        if (!string.Equals(ContentDigest.Sha256(bytes), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The process payload digest does not verify.");
        var value = JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.ProcessRecord)
            ?? throw new InvalidOperationException("A process record cannot be empty.");
        if (value.Key != key)
            throw new InvalidOperationException("The process payload belongs to a different key.");
        return new StoredRecord(value, manifest.Chunks);
    }

    static async Task WriteStoredAsync(IKvTransaction tx, ProcessRecord record, int previousChunks, bool create,
        CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, ComplianceCoreJsonContext.Default.ProcessRecord);
        if (bytes.Length > MaximumRecordBytes)
            throw new ArgumentOutOfRangeException(nameof(record), "The process record exceeds its storage budget.");
        var chunks = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
        for (var index = 0; index < chunks; index++)
            await tx.PutAsync(ChunkKey(record.Key, index), bytes.AsMemory(index * ChunkBytes,
                Math.Min(ChunkBytes, bytes.Length - index * ChunkBytes)), ct).ConfigureAwait(false);
        for (var index = chunks; index < previousChunks; index++)
            await tx.DeleteAsync(ChunkKey(record.Key, index), ct).ConfigureAwait(false);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(
            new ProcessRecordManifest(bytes.Length, chunks, ContentDigest.Sha256(bytes)),
            ComplianceCoreJsonContext.Default.ProcessRecordManifest);
        if (create)
            await tx.InsertAsync(RecordKey(record.Key), manifest, ct).ConfigureAwait(false);
        else
            await tx.PutAsync(RecordKey(record.Key), manifest, ct).ConfigureAwait(false);
    }

    sealed record StoredRecord(ProcessRecord Value, int Chunks);
}
