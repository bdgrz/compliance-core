using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>One work item's legacy assignment history was carried into its own stream.</summary>
[Discriminator("bdgrz.work.assignment-history-imported", 1)]
public sealed record WorkItemAssignmentHistoryImported(Uuid TenantId, Uuid ProgramId,
    Uuid WorkItemId, long Revision, Uuid? AssigneeMemberId, bool Escalated,
    IReadOnlyList<WorkAssignmentEntryView> History) : DomainEvent;
