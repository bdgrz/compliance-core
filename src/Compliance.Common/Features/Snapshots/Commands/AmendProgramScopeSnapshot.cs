using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Commands;

[Discriminator("bdgrz.snapshot.program_scope.amend", 1)]
public sealed record AmendProgramScopeSnapshot(Uuid TenantId, Uuid SnapshotId,
    Uuid ProgramId, long ExpectedProgramRevision, Uuid BoundaryId,
    Uuid ApprovedBoundaryVersionId, string Reason)
    : IRequest<SnapshotRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
