using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
namespace Bdgrz.Compliance.Features.Work.Projections;

/// <summary>Current native header joined with its complete verified external chronology.</summary>
public sealed record WorkAssignmentReadSnapshot(WorkItemAssignment Owner, WorkAssignmentState State,
    IReadOnlyList<WorkAssignmentChangeContent> Changes);
