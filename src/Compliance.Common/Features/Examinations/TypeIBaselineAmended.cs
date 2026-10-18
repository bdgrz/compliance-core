using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Links a new attributable baseline revision to its immutable predecessor.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-amended", 1)]
public sealed record TypeIBaselineAmended(
    Uuid TenantId,
    ExaminationEngagementView Engagement,
    TypeIBaselineView Baseline,
    DateTimeOffset OccurredAt) : DomainEvent;
