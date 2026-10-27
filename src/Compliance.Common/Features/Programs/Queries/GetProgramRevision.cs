using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.revision.get", 1)]
public sealed record GetProgramRevision(Uuid TenantId, Uuid ProgramId, long Revision)
    : IRequest<ProgramRevisionView>, IProgramReadRequest, ICallable;
