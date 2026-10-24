using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits one positive actual staff assignment to an accepted engagement by its designated partner and
///     acknowledges it with a fixed 202. <paramref name="CommandId" /> is the logical operation identity. The
///     assignment acquires the same exact service epoch, partner authority and permanent staff and canonical-user
///     practice commitments as acceptance before the engagement records it.
/// </summary>
[Discriminator("bdgrz.access-control.assign-service-engagement-staff", 1)]
public sealed record AssignServiceEngagementStaff(Uuid CommandId, Uuid TenantId, Uuid EngagementId, Uuid StaffMemberId,
    long ExpectedRevision, DateOnly EffectiveFrom)
    : IRequest<OperationAcceptedResponse>, IPersonalEngagementPartnerRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
