using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Projections;

static class ControlOperatingPlanWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<ControlOperatingPlanLineWorkState, string> Lines = new(
        "plan_lines", ComplianceCoreJsonContext.Default.ControlOperatingPlanLineWorkState,
        static line => LineKey(line.ProgramId, line.ControlId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<ControlOperatingPlanApprovalWorkState> ByProgram =
        new("by_program", 1, static work =>
            [work.ProgramId.ToString(), work.PlanVersionId.ToString()]);

    public static readonly KvDirectory<ControlOperatingPlanApprovalWorkState, string> Plans = new(
        "pending_approvals",
        ComplianceCoreJsonContext.Default.ControlOperatingPlanApprovalWorkState,
        static work => PlanKey(work.ProgramId, work.ControlId, work.PlanVersionId),
        static key => [key], [ByProgram]);

    public static string PlanKey(Uuid programId, Uuid controlId, Uuid planVersionId) =>
        $"{programId}\n{controlId}\n{planVersionId}";

    public static string LineKey(Uuid programId, Uuid controlId) =>
        $"{programId}\n{controlId}";
}
