using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class FirmStaffDirectorySchema
{
    public static readonly KvDirectoryIndex<FirmStaffMemberView> ById = new(
        "by_staff_member_id", 1, static staff => [staff.StaffMemberId.ToString()]);

    /// <summary>Current view of every registered staff record, keyed by staff member id.</summary>
    public static readonly KvDirectory<FirmStaffMemberView, Uuid> Staff = new(
        "firm-staff-by-id",
        ComplianceCoreJsonContext.Default.FirmStaffMemberView,
        static staff => staff.StaffMemberId,
        static staffMemberId => [staffMemberId.ToString()],
        [ById]);
}
