using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Aggregates;

/// <summary>Rehydrates the old program-wide stream while active writes use per-item streams.</summary>
sealed class LegacyWorkAssignmentLedger : Aggregate
{
    readonly Dictionary<Uuid, Entry> _items = [];

    public LegacyWorkAssignmentLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "work-assignments",
            programId.ToString()))
    {
        On<WorkItemAssigned>(ev =>
        {
            var entry = Get(ev.WorkItemId);
            entry.Revision = ev.Revision;
            entry.AssigneeMemberId = ev.AssigneeMemberId;
            entry.History.Add(new WorkAssignmentEntryView(ev.Revision, ev.Action,
                ev.AssigneeMemberId, ev.PreviousAssigneeMemberId, ev.Reason, ev.AssignedBy,
                ev.AssignedAt));
        });
        On<WorkItemEscalated>(ev =>
        {
            var entry = Get(ev.WorkItemId);
            entry.Revision = ev.Revision;
            entry.Escalated = true;
            entry.History.Add(new WorkAssignmentEntryView(ev.Revision,
                WorkItemAssignment.Escalate, null, null, ev.Reason, ev.EscalatedBy,
                ev.EscalatedAt));
        });
    }

    /// <summary>The recorded accountability for an item, or an empty legacy entry.</summary>
    public WorkAssignmentState Read(Uuid workItemId) =>
        _items.TryGetValue(workItemId, out var entry)
            ? new WorkAssignmentState(entry.Revision, entry.AssigneeMemberId, entry.Escalated,
                entry.History.ToArray())
            : WorkAssignmentState.Empty;

    Entry Get(Uuid workItemId)
    {
        if (!_items.TryGetValue(workItemId, out var entry))
            _items[workItemId] = entry = new Entry();
        return entry;
    }

    sealed class Entry
    {
        public long Revision { get; set; }
        public Uuid? AssigneeMemberId { get; set; }
        public bool Escalated { get; set; }
        public List<WorkAssignmentEntryView> History { get; } = [];
    }
}
