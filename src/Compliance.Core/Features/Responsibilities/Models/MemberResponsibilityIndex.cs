using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Responsibilities.Models;

sealed record MemberResponsibilityAssignments(Uuid TenantId, Uuid MemberId,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments);

static class MemberResponsibilityIndexSchema
{
    public static readonly KvDirectory<ResponsibilityAssignmentView, string> Assignments = new(
        "responsibility_assignments", ComplianceCoreJsonContext.Default.ResponsibilityAssignmentView,
        static item => AssignmentKey(item.Scope, item.AssignmentId), static key => [key], []);

    public static string AssignmentKey(ResponsibilityScope scope, Uuid assignmentId) =>
        $"{scope.RecordType}\n{scope.RecordId}\n{scope.VersionId}\n{scope.Revision.ToString(CultureInfo.InvariantCulture)}\n{assignmentId}";

    public static readonly KvDirectory<MemberResponsibilityAssignments, Uuid> Members = new(
        "member_responsibilities", ComplianceCoreJsonContext.Default.MemberResponsibilityAssignments,
        static item => item.MemberId, static id => [id.ToString()], []);
}
