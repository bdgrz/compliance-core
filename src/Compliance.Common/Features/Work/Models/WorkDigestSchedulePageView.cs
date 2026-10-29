namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Progress through a bounded page of tenant members during weekly scheduling.</summary>
public sealed record WorkDigestSchedulePageView(
    int MembersVisited,
    int DigestsScheduled,
    int MembersSkipped,
    string? NextCursor);
