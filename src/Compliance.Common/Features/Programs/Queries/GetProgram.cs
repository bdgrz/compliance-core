using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.get", 1)]
public sealed record GetProgram(Uuid TenantId, Uuid ProgramId, long? MinimumRevision = null)
    : IRequest<ProgramView>, IProgramReadRequest, ICallable;
