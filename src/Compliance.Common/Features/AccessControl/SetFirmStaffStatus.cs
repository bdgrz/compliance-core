using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Activates or deactivates a registered staff record. <paramref name="CommandId" /> is the logical operation
///     identity; <paramref name="ExpectedRevision" /> is the current revision of that staff record.
/// </summary>
[Discriminator("bdgrz.firm-staff.status.set", 1)]
public sealed record SetFirmStaffStatus(Uuid CommandId, Uuid StaffMemberId, bool IsActive, string Reason,
    long ExpectedRevision)
    : IRequest<FirmStaffMemberView>, IFirmStaffAdministrationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
