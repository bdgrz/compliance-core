using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.revision.get", 1)]
public sealed record GetProgramRevision(Uuid TenantId, Uuid ProgramId, long Revision)
    : IRequest<ProgramRevisionView>, IProgramReadRequest, ICallable;
