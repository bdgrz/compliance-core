using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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
