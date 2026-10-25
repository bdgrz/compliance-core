using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>One complete transient rules change, outside native command authority.</summary>
public sealed record ExaminationEngagementRuleRevised(Uuid TenantId, ExaminationEngagementView Engagement,
    DateTimeOffset OccurredAt) : ExaminationEngagementRuleChange(TenantId, Engagement, OccurredAt);
