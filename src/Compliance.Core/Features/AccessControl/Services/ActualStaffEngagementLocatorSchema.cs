using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class ActualStaffEngagementLocatorSchema
{
    public static readonly KvDirectoryIndex<ActualStaffEngagementLocatorView> ByStaff = new(
        "by_staff", 1, static row => [row.StaffMemberId.ToString(), row.UserId.ToString(), row.LocatorId.ToString()]);

    public static readonly KvDirectory<ActualStaffEngagementLocatorView, Uuid> Rows = new(
        "actual_staff", ComplianceCoreJsonContext.Default.ActualStaffEngagementLocatorView,
        static row => row.LocatorId, static id => [id.ToString()], [ByStaff]);
}
