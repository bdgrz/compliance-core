using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>The one successful weekly digest, with the exact keys and destination actually sent.</summary>
[Discriminator("bdgrz.work.weekly_digest.sent", 1)]
public sealed record WeeklyWorkDigestSent(Uuid TenantId, Uuid MemberId, DateOnly WeekOf,
    string TimeZoneId, Uuid MessageId, IReadOnlyList<Uuid> ItemKeys, string Destination,
    DateTimeOffset SentAt) : DomainEvent;
