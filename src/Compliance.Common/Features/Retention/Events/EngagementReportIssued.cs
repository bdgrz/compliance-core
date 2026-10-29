using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
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
