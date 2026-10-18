using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Opens a tenant-owned Type I examination engagement with its initial terms.</summary>
[Discriminator("bdgrz.examinations.examination-engagement-opened", 1)]
public sealed record ExaminationEngagementOpened(
    Uuid TenantId,
    ExaminationEngagementView Engagement,
    DateTimeOffset OccurredAt) : DomainEvent;
