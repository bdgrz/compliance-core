using System.Globalization;
using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
/// Process records on <c>kv://bdgrz/processes/{processKind}-{shard}</c> (shard = first two hex characters of the
/// root operation id, 256 per kind). Keys: <c>{realm}/p/{root}</c> holds the record; <c>due/{unixMs:D20}/{realm}/{root}</c>
/// indexes non-terminal records by due time and is maintained in the same transaction. The route lock at BEGIN
/// makes read-compare-write the CAS; a lost lock is retried from BEGIN.
/// </summary>
sealed class FitzKvProcessRecordStore(IKvClient client) : IProcessRecordStore
{
    internal const int Attempts = 4;

    public async Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var tx = await client.BeginAsync(Route(key), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var found = await tx.GetAsync(RecordKey(key), ct).ConfigureAwait(false);
        return found.Found ? Parse(found.Value!.Value.Span) : null;
    }

    public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return RetryAsync(async () =>
        {
            await using var tx = await client.BeginAsync(Route(record.Key), KvMode.ReadWrite, ct).ConfigureAwait(false);
            var found = await tx.GetAsync(RecordKey(record.Key), ct).ConfigureAwait(false);
            if (found.Found)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return Parse(found.Value!.Value.Span);
            }

            var stored = record with { Version = 1 };
            await tx.InsertAsync(RecordKey(record.Key), Serialize(stored), ct).ConfigureAwait(false);
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
            var found = await tx.GetAsync(RecordKey(record.Key), ct).ConfigureAwait(false);
            if (!found.Found)
                throw new ProcessConcurrencyException($"Process {record.Key} no longer exists.");
            var current = Parse(found.Value!.Value.Span);
            if (current.Version != expectedVersion)
                throw new ProcessConcurrencyException(
                    $"Process {record.Key} is at version {current.Version}, not {expectedVersion}.");
            var stored = record with { Version = expectedVersion + 1 };
            await tx.PutAsync(RecordKey(record.Key), Serialize(stored), ct).ConfigureAwait(false);
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
        for (var shard = 0; shard < 256 && keys.Count < limit; shard++)
        {
            await using var tx = await client.BeginAsync(Route(processKind, shard.ToString("x2", CultureInfo.InvariantCulture)),
                KvMode.ReadOnly, ct).ConfigureAwait(false);
            var page = await tx.ScanAsync(new KvScanQuery(Encoding.UTF8.GetBytes("due/"), end,
                (uint)(limit - keys.Count), false, false), ct).ConfigureAwait(false);
            foreach (var pair in page.Pairs)
            {
                var parts = Encoding.UTF8.GetString(pair.Key.Span).Split('/');
                if (parts.Length != 4)
                    continue;
                keys.Add(new ProcessKey(processKind, parts[2], Uuid.Parse(parts[3], CultureInfo.InvariantCulture)));
            }
        }

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

    static byte[] RecordKey(ProcessKey key) =>
        Encoding.UTF8.GetBytes($"{key.Realm}/p/{key.RootOperationId.ToString("N", CultureInfo.InvariantCulture)}");

    static byte[] DueKey(ProcessRecord record) => Encoding.UTF8.GetBytes(
        $"due/{Stamp(record.NextDueAt)}/{record.Key.Realm}/{record.Key.RootOperationId.ToString("N", CultureInfo.InvariantCulture)}");

    static string Stamp(DateTimeOffset at) =>
        Math.Max(0, at.ToUnixTimeMilliseconds()).ToString("D20", CultureInfo.InvariantCulture);

    static byte[] Serialize(ProcessRecord record) =>
        JsonSerializer.SerializeToUtf8Bytes(record, ComplianceCoreJsonContext.Default.ProcessRecord);

    static ProcessRecord Parse(ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.ProcessRecord)
        ?? throw new InvalidOperationException("A process record cannot be empty.");
}
