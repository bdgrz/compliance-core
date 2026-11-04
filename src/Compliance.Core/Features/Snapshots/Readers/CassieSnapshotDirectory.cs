using System.Globalization;
using Bdgrz.Compliance.Features.Programs.Readers;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
namespace Bdgrz.Compliance.Features.Snapshots.Readers;

sealed class CassieSnapshotDirectory(CassieProjectionDatabase database)
    : CassieProgramScopeRepository(database, "SnapshotDirectory", "snapshots"),
        ISnapshotDirectoryReader, ISnapshotDirectoryProjection
{
    public ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not SnapshotFrozen frozen)
            throw new ArgumentException("The snapshot directory requires a frozen snapshot event.",
                nameof(domainEvent));
        return InsertValueAsync(
            new SnapshotView(frozen.TenantId, frozen.SnapshotId, frozen.RootSnapshotId,
                frozen.AmendsSnapshotId, frozen.ProgramId, frozen.Kind, 1,
                frozen.Manifest, frozen.CanonicalManifest, frozen.ContentSha256,
                frozen.AmendmentReason, frozen.ActorMemberId, frozen.ActorDisplay,
                frozen.FrozenAt), ct);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("SnapshotDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString(), "snapshots")), ct);

    public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId, CancellationToken ct = default) =>
        ReadAsync("scope_snapshots", tenantId, snapshotId.ToString(), ComplianceCoreJsonContext.Default.SnapshotView, ct);
    public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
        PageAsync("scope_snapshots", tenantId, programId.ToString(), limit, cursor, ComplianceCoreJsonContext.Default.SnapshotView, ct);
    ValueTask InsertValueAsync(SnapshotView value, CancellationToken ct) =>
        InsertAsync("scope_snapshots", value.TenantId, value.SnapshotId.ToString(), value.ProgramId.ToString(),
            value.FrozenAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture), value, ComplianceCoreJsonContext.Default.SnapshotView, ct);
}
