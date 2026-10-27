using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
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
