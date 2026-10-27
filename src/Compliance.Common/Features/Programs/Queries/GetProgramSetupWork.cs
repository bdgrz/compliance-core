using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.setup-work.get", 1)]
public sealed record GetProgramSetupWork(Uuid TenantId, Uuid ProgramId,
    int? BoundaryLimit = null, string? BoundaryCursor = null,
    long? MinimumProgramRevision = null, Uuid? BoundaryId = null,
    long? MinimumBoundaryRevision = null)
    : IRequest<ProgramSetupWorkView>, IProgramReadRequest, ICallable;
