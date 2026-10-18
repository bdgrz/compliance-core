using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the ImpactReviewWorkLinked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.changes.impact-review-work-linked", 2)]
public sealed record ImpactReviewWorkLinked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt) : DomainEvent;
