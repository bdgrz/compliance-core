using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

static class PopulationSnapshotDirectorySchema
{
    // Newest first through an ascending scan: a descending scan bounded by a prefix returns no
    // rows from the broker (#498), so the time component is inverted instead.
    public static readonly KvDirectoryIndex<PopulationSnapshotSummary> ByKindNewestFirst = new(
        "by_kind_newest_first", 1, static snapshot =>
            [snapshot.Kind,
                (long.MaxValue - snapshot.FrozenAt.ToUniversalTime().Ticks)
                    .ToString("D20", CultureInfo.InvariantCulture),
                snapshot.SnapshotId.ToString()]);

    public static readonly KvDirectory<PopulationSnapshotSummary, Uuid> Directory = new(
        "population-snapshots", ComplianceCoreJsonContext.Default.PopulationSnapshotSummary,
        static snapshot => snapshot.SnapshotId,
        static snapshotId => [snapshotId.ToString()], [ByKindNewestFirst]);
}
