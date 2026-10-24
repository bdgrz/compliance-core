using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.successor.propose", 2)]
public sealed record ProposeControlSuccessor(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ExpectedApprovedVersionId, ControlDraftContent Content)
    : IRequest<ControlSuccessorRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
