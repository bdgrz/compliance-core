using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.revise", 1)]
public sealed record ReviseProgram(Uuid CommandId, Uuid TenantId, Uuid ProgramId, long ExpectedRevision,
    string Name, ProgramPlan Plan) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
