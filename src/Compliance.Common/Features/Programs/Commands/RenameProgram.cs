using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;

namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.rename", 1)]
public sealed record RenameProgram(Uuid CommandId, Uuid TenantId, Uuid ProgramId, long ExpectedRevision, string Name)
    : IRequest, IProgramLeadMutationRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
