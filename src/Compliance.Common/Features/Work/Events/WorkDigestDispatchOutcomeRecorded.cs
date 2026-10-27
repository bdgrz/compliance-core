using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

[Discriminator("bdgrz.work.digest_dispatch.outcome_recorded", 1)]
public sealed record WorkDigestDispatchOutcomeRecorded(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string Status, DateTimeOffset RecordedAt,
    DateTimeOffset? NextAttemptAt, string? FailureCode) : DomainEvent;
