using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Transient complete validation input; never persisted or retained by an Aggregate.</summary>
public abstract record ExaminationEngagementRuleChange(Uuid TenantId, ExaminationEngagementView Engagement,
    DateTimeOffset OccurredAt);
