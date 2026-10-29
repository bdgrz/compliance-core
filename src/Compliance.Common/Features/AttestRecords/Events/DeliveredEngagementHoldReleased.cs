using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AttestRecords.Events;

/// <summary>Records the DeliveredEngagementHoldReleased domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.attestrecords.delivered-engagement-hold-released", 1)]
public sealed record DeliveredEngagementHoldReleased(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
