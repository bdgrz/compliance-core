using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Processes.Processes;

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
