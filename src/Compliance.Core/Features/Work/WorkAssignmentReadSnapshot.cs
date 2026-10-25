namespace Bdgrz.Compliance.Features.Work;

/// <summary>Current native header joined with its complete verified external chronology.</summary>
public sealed record WorkAssignmentReadSnapshot(WorkItemAssignment Owner, WorkAssignmentState State,
    IReadOnlyList<WorkAssignmentChangeContent> Changes);
