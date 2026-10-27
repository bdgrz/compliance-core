using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Services;

static class ExaminationEngagementSourceMatch
{
    public static bool Matches(ExaminationEngagementView left, ExaminationEngagementView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.ExaminationEngagementId == right.ExaminationEngagementId &&
        left.Revision == right.Revision && left.UpdatedAt == right.UpdatedAt &&
        left.BoundaryId == right.BoundaryId && left.PeriodStart == right.PeriodStart &&
        left.PeriodEnd == right.PeriodEnd && left.EngagementType == right.EngagementType &&
        left.AuditFirm == right.AuditFirm &&
        left.TrustServicesCategories.SequenceEqual(right.TrustServicesCategories) &&
        left.ManagementContactMemberIds.SequenceEqual(right.ManagementContactMemberIds);
}
