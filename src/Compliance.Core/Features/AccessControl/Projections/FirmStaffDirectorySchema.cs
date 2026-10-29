using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

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
