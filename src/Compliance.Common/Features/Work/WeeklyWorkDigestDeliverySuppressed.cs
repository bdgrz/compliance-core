using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records why a scheduled digest was suppressed before an email attempt began.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-suppressed", 1)]
public sealed record WeeklyWorkDigestDeliverySuppressed(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    string Reason,
    ActorReference SuppressedBy,
    DateTimeOffset SuppressedAt) : DomainEvent;
