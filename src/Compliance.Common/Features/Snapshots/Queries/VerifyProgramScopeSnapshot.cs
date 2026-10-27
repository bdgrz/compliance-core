using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Queries;

[Discriminator("bdgrz.snapshot.program_scope.verify", 1)]
public sealed record VerifyProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotVerification>, ISnapshotProgramReadRequest, ICallable;
