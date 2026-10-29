using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.get", 1)]
public sealed record GetProgram(Uuid TenantId, Uuid ProgramId, long? MinimumRevision = null)
    : IRequest<ProgramView>, IProgramReadRequest, ICallable;
