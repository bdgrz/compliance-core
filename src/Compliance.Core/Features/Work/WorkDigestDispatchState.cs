using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed record WorkDigestDispatchState(DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt,
    DateTimeOffset RetryDeadline, Uuid MessageId, string Status, int Attempts,
    DateTimeOffset? LastAttemptAt, DateTimeOffset LastUpdatedAt, DateTimeOffset? NextAttemptAt,
    string? FailureCode, Uuid? RetryAuthorizedBy = null, DateTimeOffset? RetryAuthorizedAt = null,
    string? RetryEvidenceReference = null)
{
    public WorkDigestDispatchStatusView View(Uuid tenantId, Uuid memberId) =>
        new(tenantId, memberId, WeekOf, TimeZoneId, ScheduledAt, Status, Attempts,
            LastAttemptAt, MessageId, LastUpdatedAt, NextAttemptAt, FailureCode,
            RetryAuthorizedBy, RetryAuthorizedAt, RetryEvidenceReference);
}
