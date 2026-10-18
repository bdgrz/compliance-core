using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Freezes the exact baseline and advances the owning engagement lifecycle.</summary>
[Discriminator("bdgrz.examinations.type-i-baseline-frozen", 1)]
public sealed record TypeIBaselineFrozen(
    Uuid TenantId,
    ExaminationEngagementView Engagement,
    TypeIBaselineView Baseline,
    DateTimeOffset OccurredAt) : DomainEvent;
