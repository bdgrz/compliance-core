using Cntryl.Fitz;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>Checks the actual KV operation, including its route and key, before forwarding it.</summary>
sealed class PersistenceKvClient(IKvClient inner) : IKvClient
{
    public async Task<IKvTransaction> BeginAsync(string route, KvMode mode = KvMode.ReadWrite,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        FitzFrameLimits.CheckKv(route, 5); // Route length prefix and transaction mode.
        return new Transaction(await inner.BeginAsync(route, mode, ct).ConfigureAwait(false));
    }

    public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
        inner.SubscribeAsync(pattern, ct);

    sealed class Transaction(IKvTransaction inner) : IKvTransaction
    {
        public bool SupportsExclusiveScan => inner.SupportsExclusiveScan;
        public string Route => inner.Route;

        public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default)
        {
            Check(16L + key.Length, ct);
            return inner.GetAsync(key, ct);
        }

        public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, CancellationToken ct = default)
        {
            Check(20L + key.Length + value.Length, ct);
            return inner.PutAsync(key, value, ct);
        }

        public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, CancellationToken ct = default)
        {
            Check(20L + key.Length + value.Length, ct);
            return inner.InsertAsync(key, value, ct);
        }

        public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default)
        {
            Check(16L + key.Length, ct);
            return inner.DeleteAsync(key, ct);
        }

        public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
            CancellationToken ct = default)
        {
            Check(20L + startKey.Length + endKey.Length, ct);
            return inner.DeleteRangeAsync(startKey, endKey, ct);
        }

        public Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            var size = 16L + (query.StartKey is { } start ? 4L + start.Length : 0) +
                (query.EndKey is { } end ? 4L + end.Length : 0) +
                (query.Limit.HasValue ? 4 : 0) + (query.StartExclusive ? 1 : 0);
            Check(size, ct);
            return inner.ScanAsync(query, ct);
        }

        public Task CommitAsync(KvDurability durability, CancellationToken ct = default)
        {
            Check(13, ct);
            return inner.CommitAsync(durability, ct);
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            Check(12, ct);
            return inner.RollbackAsync(ct);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        void Check(long size, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            FitzFrameLimits.CheckKv(Route, size);
        }
    }
}
