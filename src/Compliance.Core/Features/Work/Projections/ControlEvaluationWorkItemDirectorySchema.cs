using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Work.Projections;

static class ControlEvaluationWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<ControlEvaluationWorkState, string> Evaluations = new(
        "evaluations", ComplianceCoreJsonContext.Default.ControlEvaluationWorkState,
        static state => EvaluationKey(state.ProgramId, state.EvaluationId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram]);

    public static string EvaluationKey(Uuid programId, Uuid evaluationId) =>
        $"{programId}\n{evaluationId}";
}
