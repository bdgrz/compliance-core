using Bdgrz.Compliance.Features.Operations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     A read-only composition of a program's control plan lines and recorded occurrences, each read from its own
///     owner stream. It decides nothing: plan approvals reassign open work by overlaying the approved reassignment
///     lists on the occurrences' materialized assignees, and expected periods that are selected but not materialized
///     are derived from the approved cadence.
/// </summary>
public sealed class ControlOperationsSnapshot
{
    readonly Dictionary<Uuid, ControlOperatingPlanLine> _lines;
    readonly Dictionary<Uuid, ControlOccurrenceView> _occurrences;

    /// <summary>Builds the composition from the plan lines and the raw occurrence views of a program.</summary>
    public ControlOperationsSnapshot(Uuid tenantId, Uuid programId, IEnumerable<ControlOperatingPlanLine> lines,
        IEnumerable<ControlOccurrenceView> occurrences)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(occurrences);
        (TenantId, ProgramId) = (tenantId, programId);
        _lines = lines.ToDictionary(static line => line.ControlId);
        _occurrences = occurrences.Select(Overlay).ToDictionary(static occurrence => occurrence.OccurrenceId);
    }

    /// <summary>The tenant of the program.</summary>
    public Uuid TenantId { get; }

    /// <summary>The program this snapshot composes.</summary>
    public Uuid ProgramId { get; }

    /// <summary>Every control that has an operating plan line.</summary>
    public IEnumerable<Uuid> PlannedControlIds => _lines.Keys;

    /// <summary>The control of a recorded occurrence, or null when it was never recorded.</summary>
    public Uuid? OccurrenceControlId(Uuid occurrenceId) =>
        _occurrences.TryGetValue(occurrenceId, out var occurrence) ? occurrence.ControlId : null;

    /// <summary>The logical revision of a control's plan line.</summary>
    public long PlanRevision(Uuid controlId) => _lines.GetValueOrDefault(controlId)?.Revision ?? 0;

    /// <summary>The latest approved plan of a control.</summary>
    public ControlOperatingPlanView? CurrentPlan(Uuid controlId) => _lines.GetValueOrDefault(controlId)?.Current;

    /// <summary>The plan awaiting approval.</summary>
    public ControlOperatingPlanView? PendingPlan(Uuid controlId) => _lines.GetValueOrDefault(controlId)?.Pending;

    /// <summary>One plan version of a control.</summary>
    public ControlOperatingPlanView? FindPlan(Uuid controlId, Uuid planVersionId) =>
        _lines.GetValueOrDefault(controlId)?.Find(planVersionId);

    /// <summary>The approved or superseded plan whose effective interval contains the date.</summary>
    public ControlOperatingPlanView? PlanOn(Uuid controlId, DateOnly date) =>
        _lines.GetValueOrDefault(controlId)?.PlanOn(date);

    /// <summary>The plan line of a control, or an empty one.</summary>
    public ControlOperatingPlanLine Line(Uuid controlId) =>
        _lines.GetValueOrDefault(controlId) ?? ControlOperatingPlanLine.Empty(controlId);

    /// <summary>The current, pending and historical plans of a control.</summary>
    public ControlOperatingPlanSetView ReadPlans(Uuid controlId) => Line(controlId).ToSetView();

    /// <summary>Every recorded occurrence in source state, without a queue horizon or derived overdue status.</summary>
    public IReadOnlyList<ControlOccurrenceView> ReadRecordedOccurrences(Uuid controlId) =>
        _occurrences.Values.Where(occurrence => occurrence.ControlId == controlId)
            .OrderBy(static occurrence => occurrence.OccurrenceId.ToString(), StringComparer.Ordinal).ToArray();

    /// <summary>Materialized occurrences whose open work would move to a new owner.</summary>
    public IReadOnlyList<OccurrenceReassignmentView> OpenWorkToReassign(Uuid controlId, OperatingHolder newOwner,
        Uuid planVersionId) => _occurrences.Values
        .Where(occurrence => occurrence.ControlId == controlId &&
                             occurrence.State is ControlOperationStates.Open or ControlOperationStates.Returned &&
                             occurrence.Assignee != newOwner)
        .OrderBy(static occurrence => occurrence.PeriodStart ?? DateOnly.MaxValue)
        .ThenBy(static occurrence => occurrence.OccurrenceId.ToString(), StringComparer.Ordinal)
        .Select(occurrence => new OccurrenceReassignmentView(occurrence.OccurrenceId, occurrence.Assignee, newOwner,
            planVersionId))
        .ToArray();

    /// <summary>Resolves an occurrence, materialized or still expected, as of the date.</summary>
    public ControlOccurrenceView? ReadOccurrence(Uuid controlId, Uuid occurrenceId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today)
    {
        if (_occurrences.TryGetValue(occurrenceId, out var occurrence))
            return occurrence.ControlId == controlId ? ControlOccurrenceFold.Present(occurrence, today) : null;
        return ExpectedOccurrences(controlId, versionUntil, today,
                today.AddDays(ControlCadenceSchedule.MaximumDueWithinDays))
            .FirstOrDefault(view => view.OccurrenceId == occurrenceId);
    }

    /// <summary>Recorded occurrences and unrecorded expected periods starting on or before the horizon.</summary>
    public IReadOnlyList<ControlOccurrenceView> ReadOccurrences(Uuid controlId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today, DateOnly horizon) =>
        _occurrences.Values.Where(occurrence => occurrence.ControlId == controlId)
            .Select(occurrence => ControlOccurrenceFold.Present(occurrence, today))
            .Concat(ExpectedOccurrences(controlId, versionUntil, today, horizon))
            .OrderBy(static view => view.DueOn ?? DateOnly.MaxValue)
            .ThenBy(static view => view.OccurrenceId.ToString(), StringComparer.Ordinal)
            .ToArray();

    /// <summary>The recurring periods of every approved plan that are not materialized yet.</summary>
    public IReadOnlyList<ControlOccurrenceView> ExpectedOccurrences(Uuid controlId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today, DateOnly horizon)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        if (!_lines.TryGetValue(controlId, out var line))
            return [];
        var views = new List<ControlOccurrenceView>();
        foreach (var plan in line.Plans.Where(static plan =>
                     plan.Status is ControlOperationStates.Approved or ControlOperationStates.Superseded))
        {
            var until = ControlOperationRules.Earliest(plan.EffectiveUntil,
                versionUntil.TryGetValue(plan.ControlVersionId, out var end) ? end : null);
            foreach (var period in ControlCadenceSchedule.Periods(plan.Cadence, plan.EffectiveFrom, until, horizon))
            {
                var occurrenceId = ControlOccurrenceIdentity.Of(TenantId, ProgramId, controlId,
                    ControlOccurrenceIdentity.WindowOf(period));
                if (_occurrences.ContainsKey(occurrenceId))
                    continue;
                views.Add(new ControlOccurrenceView(TenantId, ProgramId, controlId, occurrenceId, 0,
                    ControlOperationStates.Expected,
                    period.DueOn < today ? ControlOperationStates.Missed : ControlOperationStates.Expected,
                    plan.ControlVersionId, plan.PlanVersionId, period.Start, period.End, period.DueOn, null,
                    line.Current?.Owner ?? plan.Owner, [], [], []));
            }
        }

        return views;
    }

    ControlOccurrenceView Overlay(ControlOccurrenceView occurrence)
    {
        if (!_lines.TryGetValue(occurrence.ControlId, out var line))
            return occurrence;
        var moves = line.Plans.SelectMany(static plan => plan.Reassignments ?? [])
            .Where(move => move.OccurrenceId == occurrence.OccurrenceId).ToArray();
        return moves.Length == 0
            ? occurrence
            : occurrence with { Assignee = moves[^1].To, Reassignments = moves };
    }
}
