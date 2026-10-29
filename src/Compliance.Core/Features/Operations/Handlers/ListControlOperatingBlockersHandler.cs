using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

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
