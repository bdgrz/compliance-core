using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Owns assignment and escalation history for one source-derived work item. Completion and
///     source status remain owned by the workflow that produced the item.
/// </summary>
public sealed class WorkItemAssignment : Aggregate
{
    public const string Area = "work-item-assignments";
    public const string Claim = "claim";
    public const string Assign = "assign";
    public const string Reassign = "reassign";
    public const string Delegate = "delegate";
    public const string Escalate = "escalate";
    const int MaximumReasonLength = 2000;

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _workItemId;
    readonly List<WorkAssignmentEntryView> _history = [];
    long _revision;
    Uuid? _assigneeMemberId;
    bool _escalated;

    public WorkItemAssignment(Uuid tenantId, Uuid programId, Uuid workItemId)
        : base(IdFor(programId, workItemId), new EventStreamAddress(tenantId.ToString(), Area,
            IdFor(programId, workItemId).ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        _workItemId = workItemId;
        On<WorkItemAssignmentHistoryImported>(ev =>
        {
            RequireScope(ev.TenantId, ev.ProgramId, ev.WorkItemId);
            if (!HasValidHistory(new WorkAssignmentState(ev.Revision, ev.AssigneeMemberId,
                    ev.Escalated, ev.History)))
                throw new InvalidOperationException(
                    "Imported assignment history does not match its captured state.");
            _revision = ev.Revision;
            _assigneeMemberId = ev.AssigneeMemberId;
            _escalated = ev.Escalated;
            _history.AddRange(ev.History);
        });
        On<WorkItemAssigned>(ev =>
        {
            RequireScope(ev.TenantId, ev.ProgramId, ev.WorkItemId);
            _revision = ev.Revision;
            _assigneeMemberId = ev.AssigneeMemberId;
            _history.Add(new WorkAssignmentEntryView(ev.Revision, ev.Action,
                ev.AssigneeMemberId, ev.PreviousAssigneeMemberId, ev.Reason, ev.AssignedBy,
                ev.AssignedAt));
        });
        On<WorkItemEscalated>(ev =>
        {
            RequireScope(ev.TenantId, ev.ProgramId, ev.WorkItemId);
            _revision = ev.Revision;
            _escalated = true;
            _history.Add(new WorkAssignmentEntryView(ev.Revision, Escalate, null, null,
                ev.Reason, ev.EscalatedBy, ev.EscalatedAt));
        });
    }

    public static Uuid IdFor(Uuid programId, Uuid workItemId) =>
        Uuid.CreateVersion5(programId, $"work-item-assignment:{workItemId}");

    /// <summary>The current assignment and audit history for this item.</summary>
    public WorkAssignmentState Read() => new(_revision, _assigneeMemberId, _escalated,
        _history.ToArray());

    /// <summary>
    ///     Seeds this stream from the old program-wide ledger on the first successful mutation.
    ///     The imported event preserves the old item's complete attributable history for replay.
    /// </summary>
    public CommandFailure? ImportLegacy(WorkAssignmentState legacy)
    {
        if (legacy.Revision == 0 && legacy.History.Count == 0 &&
            legacy.AssigneeMemberId is null && !legacy.Escalated)
            return null;
        if (Version != 0 || _revision != 0)
            return CommandFailure.StateConflict(
                "Legacy assignment history can only be imported into an empty work item stream.");
        if (!HasValidHistory(legacy))
            return CommandFailure.InvalidContent(
                "Legacy assignment history does not match its current revision and state.");
        RaiseEvent(new WorkItemAssignmentHistoryImported(_tenantId, _programId, _workItemId,
            legacy.Revision, legacy.AssigneeMemberId, legacy.Escalated, legacy.History.ToArray()));
        return null;
    }

    public CommandFailure? AssignTo(long expectedRevision, string action, Uuid assigneeMemberId,
        Uuid? previousAssigneeMemberId, string? reason, ActorReference actor, DateTimeOffset at)
    {
        if (Stale(expectedRevision) is { } stale)
            return stale;
        if (reason is { } text && text.Trim().Length > MaximumReasonLength)
            return CommandFailure.InvalidContent("A reason must be at most 2000 characters.");
        RaiseEvent(new WorkItemAssigned(_tenantId, _programId, _workItemId,
            expectedRevision + 1, action, assigneeMemberId, previousAssigneeMemberId,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), actor, at));
        return null;
    }

    public CommandFailure? EscalateItem(long expectedRevision, string reason, ActorReference actor,
        DateTimeOffset at)
    {
        if (Stale(expectedRevision) is { } stale)
            return stale;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > MaximumReasonLength)
            return CommandFailure.InvalidContent(
                "An escalation needs a reason of at most 2000 characters.");
        if (_escalated)
            return CommandFailure.StateConflict("The work item is already escalated.");
        RaiseEvent(new WorkItemEscalated(_tenantId, _programId, _workItemId,
            expectedRevision + 1, reason.Trim(), actor, at));
        return null;
    }

    CommandFailure? Stale(long expectedRevision) => expectedRevision == _revision
        ? null
        : CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("work item", _revision));

    static bool HasValidHistory(WorkAssignmentState state)
    {
        if (state.Revision <= 0 || state.History.Count != state.Revision)
            return false;
        for (var index = 0; index < state.History.Count; index++)
            if (state.History[index].Revision != index + 1)
                return false;
        var lastAssignment = state.History.LastOrDefault(entry => entry.Action != Escalate);
        return (lastAssignment?.AssigneeMemberId == state.AssigneeMemberId) &&
               state.History.Any(entry => entry.Action == Escalate) == state.Escalated;
    }

    void RequireScope(Uuid tenantId, Uuid programId, Uuid workItemId)
    {
        if (tenantId != _tenantId || programId != _programId || workItemId != _workItemId)
            throw new InvalidOperationException(
                "Work assignment event scope does not match its aggregate stream.");
    }
}
