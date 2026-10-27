using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>One weekly digest's scalar current authority; complete attempts are external read history.</summary>
public sealed record WeeklyWorkDigestHeader(Uuid MemberId, DateOnly WeekStarting, string? ScheduleKey,
    DateTimeOffset? ScheduledAt, ActorReference? ScheduledBy, string Status, long Revision, string? SuppressionReason);
