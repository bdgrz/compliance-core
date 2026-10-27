using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
/// Inspects tenant events after the ApplicationDirectoryV2 checkpoint. The scan stops at
/// <see cref="ScanLimit"/> so a lagging worker costs one bounded read, never tenant history.
/// </summary>
public static class ApplicationDirectoryBacklog
{
    public const int ScanLimit = 256;

    public static async ValueTask<ApplicationDirectoryBacklogScan> FindAsync(
        IApplicationDirectoryReader directory, IDomainEventReader events, Uuid tenantId,
        Func<DomainEvent, bool> match, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        var scanned = 0;
        await foreach (var pending in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString()), checkpoint.Cursor, ct).ConfigureAwait(false))
        {
            // Exceeded means a record exists beyond the limit, not that the limit was reached.
            if (scanned++ == ScanLimit)
                return new ApplicationDirectoryBacklogScan(null, true);
            if (match(pending.Event))
                return new ApplicationDirectoryBacklogScan(pending.Event, false);
        }
        return default;
    }
}
