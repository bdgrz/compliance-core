using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Redacted delivery state for one tenant member's weekly digest.</summary>
public sealed record WorkDigestDispatchStatusView(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt, string Status,
    int Attempts, DateTimeOffset? LastAttemptAt, Uuid MessageId,
    DateTimeOffset LastUpdatedAt, DateTimeOffset? NextAttemptAt, string? FailureCode,
    Uuid? RetryAuthorizedBy = null, DateTimeOffset? RetryAuthorizedAt = null,
    string? RetryEvidenceReference = null)
{
    /// <summary>Logical revision of the complete member dispatch stream at this read or original authorization.</summary>
    public long Revision { get; init; }
}
