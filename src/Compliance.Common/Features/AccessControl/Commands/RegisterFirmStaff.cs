using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Registers one firm staff member. <paramref name="StaffMemberId" /> is the logical operation identity, so an exact
///     retry replays the original 202 acknowledgment. <paramref name="ExpectedRevision" /> is the revision of the new
///     staff record and must be zero.
/// </summary>
[Discriminator("bdgrz.firm-staff.register", 1)]
public sealed record RegisterFirmStaff(Uuid StaffMemberId, Uuid UserId, string Practice, string SourceReference,
    long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IFirmStaffAdministrationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => StaffMemberId;
}
