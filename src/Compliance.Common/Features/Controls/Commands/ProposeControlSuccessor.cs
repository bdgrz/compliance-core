using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Commands;

[Discriminator("bdgrz.control.successor.propose", 2)]
public sealed record ProposeControlSuccessor(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ExpectedApprovedVersionId, ControlDraftContent Content)
    : IRequest<ControlSuccessorRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
