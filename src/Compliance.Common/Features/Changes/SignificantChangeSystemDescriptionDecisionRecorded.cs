using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records an approved successor or no-change disposition for a significant change's description impact.</summary>
[Discriminator("bdgrz.changes.significant-change-system-description-decision-recorded", 1)]
public sealed record SignificantChangeSystemDescriptionDecisionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt) : DomainEvent;
