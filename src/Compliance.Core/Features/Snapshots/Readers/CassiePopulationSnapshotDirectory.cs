using System.Globalization;
using Bdgrz.Compliance.Features.Programs.Readers;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
namespace Bdgrz.Compliance.Features.Snapshots.Readers;

sealed class CassiePopulationSnapshotDirectory(CassieProjectionDatabase database)
    : CassieProgramScopeRepository(database, "PopulationSnapshotDirectoryV2", "population-snapshots"),
        IPopulationSnapshotDirectoryReader, IPopulationSnapshotDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("PopulationSnapshotDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "population-snapshots")), ct);

    public ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not PopulationSnapshotFrozen frozen)
            throw new ArgumentException("The population snapshot directory requires a frozen snapshot event.",
                nameof(domainEvent));
        return InsertValueAsync(
            new PopulationSnapshotSummary(frozen.TenantId, frozen.SnapshotId, frozen.RootSnapshotId,
                frozen.AmendsSnapshotId, frozen.Kind, frozen.RowCount, frozen.ContentSha256,
                frozen.AmendmentReason, frozen.FrozenBy, frozen.FrozenAt), ct);
    }

    public ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind, int limit, string? cursor, CancellationToken ct = default) =>
        PageAsync("population_snapshots", tenantId, kind, limit, cursor, ComplianceCoreJsonContext.Default.PopulationSnapshotSummary, ct);
    ValueTask InsertValueAsync(PopulationSnapshotSummary value, CancellationToken ct) =>
        InsertAsync("population_snapshots", value.TenantId, value.SnapshotId.ToString(), value.Kind,
            (long.MaxValue - value.FrozenAt.UtcTicks).ToString("D20", CultureInfo.InvariantCulture), value, ComplianceCoreJsonContext.Default.PopulationSnapshotSummary, ct);
}
