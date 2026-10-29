using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Work.Projections;

static class ControlCriterionMappingWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<ControlCriterionMappingWorkState> StatesByProgram = new(
        "by_program", 1, static state => [state.ProgramId.ToString(), state.LineId.ToString()]);

    public static readonly KvDirectory<ControlCriterionMappingWorkState, string> Mappings = new(
        "mappings", ComplianceCoreJsonContext.Default.ControlCriterionMappingWorkState,
        static state => MappingKey(state.ProgramId, state.LineId),
        static key => [key], [StatesByProgram]);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram]);

    public static string MappingKey(Uuid programId, Uuid lineId) =>
        $"{programId}\n{lineId}";
}
