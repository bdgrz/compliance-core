using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.Examinations.Rules;

/// <summary>One complete transient rules change, outside native command authority.</summary>
public sealed record ExaminationEngagementRuleFrozen(Uuid TenantId, ExaminationEngagementView Engagement,
    TypeIBaselineView Baseline, DateTimeOffset OccurredAt) : ExaminationEngagementRuleChange(TenantId, Engagement, OccurredAt);
