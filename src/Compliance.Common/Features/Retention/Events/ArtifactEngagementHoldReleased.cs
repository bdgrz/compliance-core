using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
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
