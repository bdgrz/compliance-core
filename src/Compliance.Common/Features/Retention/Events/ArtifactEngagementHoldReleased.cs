using Bdgrz.Compliance.Features.Retention.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Events;

/// <summary>Records the ArtifactEngagementHoldReleased domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-engagement-hold-released", 1)]
public sealed record ArtifactEngagementHoldReleased(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ArtifactEngagementHoldView State,
    DateTimeOffset OccurredAt) : DomainEvent;
