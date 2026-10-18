using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one deterministic email delivery attempt for a scheduled weekly digest.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-requested", 1)]
public sealed record WeeklyWorkDigestDeliveryRequested(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    int AttemptNumber,
    string DeliveryKey,
    ActorReference RequestedBy,
    DateTimeOffset RequestedAt) : DomainEvent;
