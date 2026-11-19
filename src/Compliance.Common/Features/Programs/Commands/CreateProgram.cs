using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.create", 1)]
public sealed record CreateProgram(Uuid ProgramId, Uuid TenantId, string Name, ProgramPlan Plan,
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Uuid EditionId = default)
    : IRequest<ProgramRegistration>, IProgramManagementRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ProgramId;
}
