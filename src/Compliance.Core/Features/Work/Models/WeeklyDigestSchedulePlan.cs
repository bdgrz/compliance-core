using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>The bounded membership selection accepted before any child digest is scheduled.</summary>
public sealed record WeeklyDigestSchedulePlan(Uuid TenantId, Uuid PageId, DateOnly WeekStarting,
    string ScheduleKey, DateTimeOffset AcceptedAt, int MembersVisited, int MembersSkipped,
    IReadOnlyList<Uuid> MemberIds, string? NextCursor);
