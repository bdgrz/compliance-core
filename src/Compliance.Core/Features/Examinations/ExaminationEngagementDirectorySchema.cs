using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

static class ExaminationEngagementDirectorySchema
{
    public static readonly KvDirectory<ExaminationEngagementView, Uuid> Directory = new(
        "examination_engagements", ComplianceCoreJsonContext.Default.ExaminationEngagementView,
        static engagement => engagement.ExaminationEngagementId,
        static engagementId => [engagementId.ToString()], []);

    public static readonly KvDirectoryIndex<TypeIBaselineView> ByProgramBaseline = new("by_program_baseline", 1,
        static baseline => [baseline.ProgramId.ToString(), baseline.TypeIBaselineId.ToString(), baseline.ExaminationEngagementId.ToString()]);

    public static readonly KvDirectory<TypeIBaselineView, string> Baselines = new(
        "type_i_baselines", ComplianceCoreJsonContext.Default.TypeIBaselineView,
        static baseline => BaselineKey(baseline.ExaminationEngagementId, baseline.TypeIBaselineId),
        static key => [key], [ByProgramBaseline]);

    public static string BaselineKey(Uuid engagementId, Uuid baselineId) => $"{engagementId}:{baselineId}";
}
