using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Revokes one actual assignment of an accepted service engagement at its current revision.</summary>
[Discriminator("bdgrz.service-engagement.actual-staff.revoke", 1)]
public sealed record RevokeServiceEngagementActualStaff(Uuid CommandId, Uuid TenantId, Uuid EngagementId,
    Uuid StaffMemberId, long ExpectedRevision, string Reason)
    : IRequest<ServiceEngagementAcceptanceView>, IIndependenceAdministrationRequest,
        IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
