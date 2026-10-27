using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

/// <summary>Verifies a population snapshot's retained amendment chain within a link budget.</summary>
static class PopulationSnapshotLineage
{
    public static async ValueTask<bool> IsBoundedAsync(IAggregateReader reader, Uuid tenantId,
        PopulationSnapshot leaf, int maximumLinks, CancellationToken ct)
    {
        var seen = new HashSet<Uuid> { leaf.Id };
        var current = leaf;
        for (var links = 0; current.AmendsSnapshotId is { } predecessorId; links++)
        {
            if (links >= maximumLinks || !seen.Add(predecessorId))
                return false;
            var predecessor = await reader.HydrateAsync(
                new PopulationSnapshot(tenantId, predecessorId), ct).ConfigureAwait(false);
            if (!predecessor.IsFrozen || predecessor.Kind != leaf.Kind ||
                predecessor.RootSnapshotId != leaf.RootSnapshotId)
                return false;
            current = predecessor;
        }
        return current.Id == leaf.RootSnapshotId;
    }
}
