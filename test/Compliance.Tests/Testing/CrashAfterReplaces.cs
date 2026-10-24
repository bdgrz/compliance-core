using Bdgrz.Compliance.Features.Processes;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Crashes a worker after a number of successful process-record compare-and-swaps, so a test can stop a driver in
///     the middle of a process and resume it from the durable record.
/// </summary>
sealed class CrashAfterReplaces
{
    int _remaining = int.MaxValue;

    public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

    public void Disarm() => _remaining = int.MaxValue;

    public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

    sealed class Decorator(IProcessRecordStore inner, CrashAfterReplaces owner) : IProcessRecordStore
    {
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
            inner.CreateIfAbsentAsync(record, ct);

        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
        {
            if (owner._remaining-- <= 0)
                throw new InvalidOperationException("Worker crashed before the CAS.");
            return inner.ReplaceAsync(record, expectedVersion, ct);
        }

        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
    }
}
