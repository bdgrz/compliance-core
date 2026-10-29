using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
namespace Bdgrz.Compliance.Features.Remediation.Services;

/// <summary>
///     Derives the status of a finding view from its parts and applies one accepted fact to a view, so the frozen
///     first response of a command is exactly the view the fact produces.
/// </summary>
static class FindingViews
{
    /// <summary>Recomputes status and readiness status at <paramref name="now" />.</summary>
    public static FindingView Settle(FindingView view, DateTimeOffset now)
    {
        var acceptances = view.Acceptances.Select(acceptance => acceptance with
        {
            State = acceptance.ExpiresAt > now ? "active" : "expired",
        }).ToArray();
        var remediated = view.CorrectiveActions.Count > 0 &&
                         view.CorrectiveActions.All(static action => action.Status == FindingStates.Completed);
        var status = view.Closure is not null ? FindingStates.Closed
            : remediated ? FindingStates.Remediated
            : FindingStates.Open;
        var readiness = view.Closure is not null ? FindingStates.Closed
            : remediated ? FindingStates.Remediated
            : acceptances.Any(static acceptance => acceptance.State == "active") ? FindingStates.Accepted
            : view.DueOn < DateOnly.FromDateTime(now.UtcDateTime) ? FindingStates.Overdue
            : FindingStates.Unresolved;
        return view with { Status = status, ReadinessStatus = readiness, Acceptances = acceptances };
    }

    public static FindingView WithRevision(FindingView view, FindingRevision revision, long number, DateTimeOffset now) =>
        Settle(view with
        {
            Revision = number,
            Severity = revision.Severity,
            OwnerMemberId = revision.OwnerMemberId,
            DueOn = revision.DueOn,
            AffectedScope = revision.AffectedScope,
            RootCause = revision.RootCause,
            History = [.. view.History, new FindingHistoryEntryView(number, "revised",
                $"Severity {revision.Severity}, owner {revision.OwnerMemberId}, due {Date(revision.DueOn)}: {revision.Reason}",
                revision.RevisedBy, revision.RevisedAt)],
        }, now);

    public static FindingView WithAcceptance(FindingView view, FindingAcceptanceView acceptance, long number,
        DateTimeOffset now) =>
        Settle(view with
        {
            Revision = number,
            Acceptances = [.. view.Acceptances, acceptance],
            History = [.. view.History, new FindingHistoryEntryView(number, "acceptance_linked",
                $"{acceptance.Kind} {acceptance.RecordId} until {acceptance.ExpiresAt:O}", acceptance.LinkedBy,
                acceptance.LinkedAt)],
        }, now);

    public static FindingView WithClosure(FindingView view, FindingClosureView closure, long number, DateTimeOffset now) =>
        Settle(view with
        {
            Revision = number,
            Closure = closure,
            History = [.. view.History, new FindingHistoryEntryView(number, "closed",
                $"Decision {closure.DecisionId}: {closure.Rationale}", closure.ClosedBy, closure.ClosedAt)],
        }, now);

    public static FindingView WithReopen(FindingView view, string reason, long number,
        ActorReference actor, DateTimeOffset at, DateTimeOffset now) =>
        Settle(view with
        {
            Revision = number,
            Closure = null,
            History = [.. view.History, new FindingHistoryEntryView(number, "reopened", reason, actor, at)],
        }, now);

    public static FindingView WithCompletedAction(FindingView view, CorrectiveActionView action, DateTimeOffset now) =>
        Settle(view with
        {
            CorrectiveActions = view.CorrectiveActions.Select(item => item.ActionId == action.ActionId ? action : item)
                .ToArray(),
            History = [.. view.History, new FindingHistoryEntryView(action.Revision, "corrective_action_completed",
                action.ResolutionNotes!, action.CompletedBy!, action.CompletedAt!.Value)],
        }, now);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
