using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Queries;

[Discriminator("bdgrz.snapshot.program_scope.verify", 1)]
public sealed record VerifyProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotVerification>, ISnapshotProgramReadRequest, ICallable;
