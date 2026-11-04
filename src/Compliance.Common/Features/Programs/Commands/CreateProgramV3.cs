using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;

namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.create", 3)]
public sealed record CreateProgramV3(Uuid ProgramId, Uuid TenantId, string Name, ProgramPlan Plan, Uuid EditionId)
    : IRequest<ProgramRegistration>, IProgramLeadMutationRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ProgramId;
}
