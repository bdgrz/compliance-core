using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Queries;

[Discriminator("bdgrz.snapshot.get", 1)]
public sealed record GetSnapshot(Uuid TenantId, Uuid SnapshotId, long? MinimumRevision = null)
    : IRequest<SnapshotView>, ISnapshotProgramReadRequest, ICallable;
