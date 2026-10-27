using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.revise", 1)]
public sealed record ReviseProgram(Uuid CommandId, Uuid TenantId, Uuid ProgramId, long ExpectedRevision,
    string Name, ProgramPlan Plan) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
