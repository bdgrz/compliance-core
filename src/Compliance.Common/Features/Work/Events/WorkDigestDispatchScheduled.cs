using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

[Discriminator("bdgrz.work.digest_dispatch.scheduled", 1)]
public sealed record WorkDigestDispatchScheduled(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt, Uuid MessageId,
    DateTimeOffset RetryDeadline, DateTimeOffset RecordedAt) : DomainEvent;
