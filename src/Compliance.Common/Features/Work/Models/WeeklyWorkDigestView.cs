using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Delivery state for one tenant member and one Monday-starting work week.</summary>
public sealed record WeeklyWorkDigestView(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    DateOnly WeekStarting,
    string Status,
    long Revision,
    string? ScheduleKey,
    DateTimeOffset? ScheduledAt,
    ActorReference? ScheduledBy,
    IReadOnlyList<WorkDigestDeliveryAttemptView> DeliveryAttempts,
    string? SuppressionReason);

public sealed record WorkDigestDeliveryAttemptView(
    int Number,
    string DeliveryKey,
    string Status,
    DateTimeOffset RequestedAt,
    ActorReference RequestedBy,
    DateTimeOffset? CompletedAt,
    ActorReference? RecordedBy,
    string? FailureCode);
