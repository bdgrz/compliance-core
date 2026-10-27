using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record ProgramScopeSnapshotVerification(Uuid TenantId, Uuid SnapshotId,
    bool Verified, SnapshotSourceCheck Snapshot, SnapshotSourceCheck ProgramRevision,
    SnapshotSourceCheck ApprovedBoundaryVersion);
