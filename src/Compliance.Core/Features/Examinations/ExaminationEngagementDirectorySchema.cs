using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

static class ExaminationEngagementDirectorySchema
{
    public static readonly KvDirectory<ExaminationEngagementView, Uuid> Directory = new(
        "examination_engagements", ComplianceCoreJsonContext.Default.ExaminationEngagementView,
        static engagement => engagement.ExaminationEngagementId,
        static engagementId => [engagementId.ToString()], []);

    public static readonly KvDirectory<TypeIBaselineView, Uuid> Baselines = new(
        "type_i_baselines", ComplianceCoreJsonContext.Default.TypeIBaselineView,
        static baseline => baseline.TypeIBaselineId,
        static baselineId => [baselineId.ToString()], []);
}
