using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.create", 1)]
public sealed record CreateProgram(Uuid ProgramId, Uuid TenantId, string Name, ProgramPlan Plan)
    : IRequest<ProgramRegistration>, IProgramManagementRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ProgramId;
}
