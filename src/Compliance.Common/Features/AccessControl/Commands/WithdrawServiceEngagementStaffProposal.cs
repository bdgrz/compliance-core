using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Withdraws the current tentative proposal of one staff member from the open draft of a service engagement.</summary>
[Discriminator("bdgrz.service-engagement.staff.withdraw", 1)]
public sealed record WithdrawServiceEngagementStaffProposal(Uuid CommandId, Uuid TenantId, Uuid EngagementId,
    Uuid StaffMemberId, long ExpectedRevision, string Reason)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
