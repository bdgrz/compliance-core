using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.SourceFacts.Readers;

/// <summary>
/// Backend-internal reader that captures one committed global cut and then reads required native facts at or below
/// it. It never appends and grants no authority; it is not a new broker snapshot guarantee.
/// </summary>
public interface IAuthoritativeSnapshotReader
{
    /// <summary>Captures the exclusive global frontier from an unfiltered global read. Zero is a valid frontier.</summary>
    /// <exception cref="SourceFactVerificationException">The broker did not report a watermark.</exception>
    Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default);

    /// <summary>
    /// Scans one global selector from <paramref name="cursor"/> (or the beginning) until coverage reaches the cut,
    /// keeping only records of <paramref name="tenantScope"/> with a global position below the cut.
    /// </summary>
    /// <exception cref="SourceFactVerificationException">
    /// Coverage is incomplete: a filtered or missing relevant record, a snapshot older than the cut, a cursor from
    /// another selector, or a record without offsets.
    /// </exception>
    Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope, AuthoritativeReadCut cut,
        SnapshotCursor? cursor = null, CancellationToken ct = default);
}
