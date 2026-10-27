using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Tentatively proposes one active staff member for the open draft of a service engagement.</summary>
[Discriminator("bdgrz.service-engagement.staff.propose", 1)]
public sealed record ProposeServiceEngagementStaff(Uuid CommandId, Uuid TenantId, Uuid EngagementId,
    Uuid StaffMemberId, long ExpectedRevision)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
