using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Commands;

[Discriminator("bdgrz.snapshot.program_scope.amend", 1)]
public sealed record AmendProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId,
    Uuid ProgramId, long ExpectedProgramRevision, Uuid BoundaryId,
    Uuid ApprovedBoundaryVersionId, string Reason)
    : IRequest<SnapshotRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
