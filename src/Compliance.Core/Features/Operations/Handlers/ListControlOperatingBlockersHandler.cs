using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Evaluates every active approved control in the program against its operating plan from
///     source streams: missing or pending plans, plans on a superseded version, inactive holders,
///     and missed occurrences are all visible blockers.
/// </summary>
public sealed class ListControlOperatingBlockersHandler(ControlReader controlSources,
    ControlOperationsReader operations, IControlDraftDirectoryReader directory, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<ListControlOperatingBlockers, Page<ControlOperatingBlockerView>>
{
    public ValueTask<Result<Page<ControlOperatingBlockerView>>> HandleAsync(
        IRequestContext<ListControlOperatingBlockers> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<Page<ControlOperatingBlockerView>>> HandleCoreAsync(
        IRequestContext<ListControlOperatingBlockers> context, CancellationToken ct)
    {
        var request = context.Request;
        var today = ControlOperationsSource.Today(clock);
        var ledger = await operations.ReadAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        var controlIds = new List<Uuid>();
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                ControlActivationSource.MaximumPageSize, cursor, ct).ConfigureAwait(false);
            controlIds.AddRange(page.Items.Select(static row => row.ControlId));
            cursor = page.NextCursor;
        } while (cursor is not null);
        // The directory may lag; a control the ledger already plans is always evaluated.
        controlIds.AddRange(ledger.PlannedControlIds);
        var blockers = new List<ControlOperatingBlockerView>();
        foreach (var controlId in controlIds.Distinct().OrderBy(static id => id.ToString(),
                     StringComparer.Ordinal))
        {
            var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, controlId, ct).ConfigureAwait(false);
            if (control is null || control.IsRetired ||
                control.ApprovedVersion is not { Status: ControlOperationStates.Approved } version)
                continue;
            blockers.AddRange(await EvaluateAsync(request.TenantId, ledger, control, version,
                today, ct).ConfigureAwait(false));
        }
        return ControlActivationSource.Paginate(blockers, request.Limit, request.Cursor,
            "control operating blockers");
    }

    async ValueTask<IReadOnlyList<ControlOperatingBlockerView>> EvaluateAsync(Uuid tenantId,
        ControlOperationsSnapshot ledger, ControlReadSnapshot control, ControlVersionView version,
        DateOnly today, CancellationToken ct)
    {
        var identifier = version.Identifier;
        var blockers = new List<ControlOperatingBlockerView>();
        ControlOperatingBlockerView Blocker(string kind, string explanation,
            Uuid? occurrenceId = null) => new(control.Id, identifier, version.VersionId, kind,
            explanation, occurrenceId);
        if (ledger.PendingPlan(control.Id) is not null)
            blockers.Add(Blocker("pending_approval",
                "An operating plan is awaiting independent approval."));
        if (ledger.CurrentPlan(control.Id) is not { } plan)
        {
            if (blockers.Count == 0)
                blockers.Add(Blocker("missing_plan",
                    "The active control has no approved owner, reviewer, and cadence."));
            return blockers;
        }
        if (plan.ControlVersionId != version.VersionId)
            blockers.Add(Blocker("plan_for_superseded_version",
                "The approved plan targets a superseded control version; approve a plan for the current version."));
        if (!await authority.IsActiveAsync(tenantId, plan.Owner, ct).ConfigureAwait(false))
            blockers.Add(Blocker("owner_inactive",
                "The control owner is no longer an active member, person, or team."));
        if (plan.BackupOwner is { } backup &&
            !await authority.IsActiveAsync(tenantId, backup, ct).ConfigureAwait(false))
            blockers.Add(Blocker("backup_owner_inactive",
                "The backup owner is no longer an active member, person, or team."));
        if (!await authority.IsActiveAsync(tenantId,
                new OperatingHolder(OperatingAuthority.MemberHolder, plan.ReviewerMemberId), ct)
                .ConfigureAwait(false))
            blockers.Add(Blocker("reviewer_inactive", "The reviewer is no longer an active member."));
        foreach (var missed in ledger.ReadOccurrences(control.Id,
                         OperatingAuthority.VersionWindows(control), today, today)
                     .Where(static occurrence => occurrence.State == ControlOperationStates.Missed))
            blockers.Add(Blocker("missed_occurrence",
                $"The occurrence due {missed.DueOn:yyyy-MM-dd} was not performed.",
                missed.OccurrenceId));
        return blockers;
    }
}
