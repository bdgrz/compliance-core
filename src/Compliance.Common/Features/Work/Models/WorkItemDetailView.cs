using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>A work item and its assignment history, oldest first.</summary>
public sealed record WorkItemDetailView(WorkQueueItemView Item,
    IReadOnlyList<WorkAssignmentEntryView> History);
