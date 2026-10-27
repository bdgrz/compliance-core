using Bdgrz.Compliance.Features.Retention.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Events;

/// <summary>Records the EngagementReportIssued domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.engagement-report-issued", 1)]
public sealed record EngagementReportIssued(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ArtifactEngagementHoldView State,
    DateTimeOffset OccurredAt) : DomainEvent;
