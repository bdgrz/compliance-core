namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Durable CAS store for process records.</summary>
public interface IProcessRecordStore
{
    /// <summary>Reads the current record, or null when none exists.</summary>
    Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default);

    /// <summary>Creates the record when absent and returns the stored record either way.</summary>
    Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default);

    /// <summary>Replaces the record when its stored version equals <paramref name="expectedVersion"/>.</summary>
    /// <exception cref="ProcessConcurrencyException">The stored version differs or the record is missing.</exception>
    Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default);

    /// <summary>Keys of non-terminal records of one kind due at or before <paramref name="dueBefore"/>.</summary>
    Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
        CancellationToken ct = default);
}
