using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

static class ExaminationEngagementDirectorySchema
{
    public static readonly KvDirectory<ExaminationEngagementView, Uuid> Directory = new(
        "examination_engagements", ComplianceCoreJsonContext.Default.ExaminationEngagementView,
        static engagement => engagement.ExaminationEngagementId,
        static engagementId => [engagementId.ToString()], []);
}
