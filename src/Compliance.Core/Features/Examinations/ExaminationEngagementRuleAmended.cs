using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>One complete transient rules change, outside native command authority.</summary>
public sealed record ExaminationEngagementRuleAmended(Uuid TenantId, ExaminationEngagementView Engagement,
    TypeIBaselineView Baseline, DateTimeOffset OccurredAt) : ExaminationEngagementRuleChange(TenantId, Engagement, OccurredAt);
