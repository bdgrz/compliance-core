using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>A failed current eligibility check or empty digest ended this member's week without sending.</summary>
[Discriminator("bdgrz.work.weekly_digest.skipped", 1)]
public sealed record WeeklyWorkDigestSkipped(Uuid TenantId, Uuid MemberId, DateOnly WeekOf,
    string TimeZoneId, string Reason, DateTimeOffset SkippedAt) : DomainEvent;
