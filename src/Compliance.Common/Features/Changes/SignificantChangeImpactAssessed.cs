using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the SignificantChangeImpactAssessed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.changes.significant-change-impact-assessed", 2)]
public sealed record SignificantChangeImpactAssessed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt) : DomainEvent;
