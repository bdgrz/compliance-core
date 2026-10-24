using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Decorates a process record store so the worker crashes before a chosen compare-and-swap, as a process kill
///     between a receiver commit and its checkpoint would. The decorated store is the only one the driver sees.
/// </summary>
sealed class CrashingProcessStore
{
    int _remaining = int.MaxValue;

    /// <summary>Lets this many replaces succeed, then throws before the next one.</summary>
    public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

    public void Disarm() => _remaining = int.MaxValue;

    public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

    sealed class Decorator(IProcessRecordStore inner, CrashingProcessStore owner) : IProcessRecordStore
    {
        public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

        public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
            inner.CreateIfAbsentAsync(record, ct);

        public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
        {
            if (owner._remaining-- == 0)
                throw new InvalidOperationException("Worker crashed before the CAS.");
            return inner.ReplaceAsync(record, expectedVersion, ct);
        }

        public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
            CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
    }
}
