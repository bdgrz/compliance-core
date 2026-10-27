using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Commands;

[Discriminator("bdgrz.snapshot.program_scope.freeze", 1)]
public sealed record FreezeProgramScopeSnapshot(Uuid TenantId, Uuid ProgramId,
    long ExpectedProgramRevision, Uuid BoundaryId, Uuid ApprovedBoundaryVersionId)
    : IRequest<SnapshotRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
