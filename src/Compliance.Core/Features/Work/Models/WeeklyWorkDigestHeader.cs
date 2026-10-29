using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>One weekly digest's scalar current authority; complete attempts are external read history.</summary>
public sealed record WeeklyWorkDigestHeader(Uuid MemberId, DateOnly WeekStarting, string? ScheduleKey,
    DateTimeOffset? ScheduledAt, ActorReference? ScheduledBy, string Status, long Revision, string? SuppressionReason);
