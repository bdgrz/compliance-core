using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.History;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Projections;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Readers;

/// <summary>
///     Reads findings from their own owner streams and composes the public <see cref="FindingView" />. The view is
///     assembled from the finding stream, the verified admission content of every admitted action and the stream of each
///     materialized action; an admitted action that is not materialized yet is an open action at revision zero. Nothing
///     is read from a legacy ledger, and nothing is retained as command state.
/// </summary>
public sealed class FindingReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    /// <summary>Composes the view of a hydrated finding, or null when it was not raised.</summary>
    public async ValueTask<FindingView?> ComposeAsync(FindingAggregate finding, DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (finding.Raised is not { } raised)
            return null;
        var tenant = raised.TenantId;
        var links = await FindingContent.ReadAsync<IReadOnlyList<FindingLink>>(content, tenant, raised.LinksManifest, ct)
            .ConfigureAwait(false);
        var actions = new List<CorrectiveActionView>();
        var completions = new List<(CorrectiveActionView Action, long Order)>();
        var actionChanges = new List<FindingHistoryEntryView>();
        var acceptances = new List<FindingAcceptanceView>();
        var history = new List<(FindingHistoryEntryView Entry, int Order)>();
        FindingClosureView? closure = null;
        var order = 0;
        var source = await FindingHistory.ReadAsync(events, finding, ct).ConfigureAwait(false);
        foreach (var fact in source.Facts)
        {
            switch (fact)
            {
                case FindingRaisedV2 ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "raised", ev.Title, ev.RaisedBy, ev.RaisedAt),
                        order));
                    break;
                case FindingRevisedV2 ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "revised",
                        $"Severity {ev.Severity}, owner {ev.OwnerMemberId}, due {FindingViews.Date(ev.DueOn)}: {ev.Reason}",
                        ev.RevisedBy, ev.RevisedAt), order));
                    break;
                case FindingAnalysisRevised ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "analysis_revised", ev.Analysis,
                        ev.RevisedBy, ev.RevisedAt), order));
                    break;
                case CorrectiveActionAdmitted ev:
                    var action = await ComposeActionAsync(finding, ev, actionChanges, ct).ConfigureAwait(false);
                    actions.Add(action.View);
                    history.Add((new FindingHistoryEntryView(ev.Revision, "corrective_action_added",
                        action.AddedDescription, action.View.AddedBy, action.View.AddedAt), order));
                    if (action.Completion is { } completion)
                        completions.Add((completion, order));
                    break;
                case FindingAcceptanceLinkedV2 ev:
                    var acceptance = await FindingContent.ReadAsync<FindingAcceptanceView>(content, tenant,
                        ev.AcceptanceContent, ct).ConfigureAwait(false);
                    acceptances.Add(acceptance);
                    history.Add((new FindingHistoryEntryView(ev.Revision, "acceptance_linked",
                        $"{acceptance.Kind} {acceptance.RecordId} until {acceptance.ExpiresAt:O}", acceptance.LinkedBy,
                        acceptance.LinkedAt), order));
                    break;
                case FindingCorrectiveActionResolved ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "corrective_action_resolved",
                        $"Action {ev.ActionId}: {ev.Outcome}", ev.Operation.Actor, ev.Operation.AcceptedAt), order));
                    break;
                case FindingClosureProposed ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "closure_proposed",
                        $"Closure {ev.ClosureId}: {ev.Terms.Rationale}", ev.ProposedBy, ev.ProposedAt), order));
                    break;
                case FindingClosureWithdrawn ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "closure_withdrawn",
                        $"Closure {ev.ClosureId} withdrawn.", ev.WithdrawnBy, ev.WithdrawnAt), order));
                    break;
                case FindingClosureReturned ev:
                    history.Add((new FindingHistoryEntryView(ev.Revision, "closure_returned", ev.Comments,
                        ev.Review.ReviewedBy, ev.Review.ReviewedAt), order));
                    break;
                case FindingClosedV2 ev:
                    closure = await FindingContent.ReadAsync<FindingClosureView>(content, tenant, ev.ClosureContent, ct)
                        .ConfigureAwait(false);
                    history.Add((new FindingHistoryEntryView(ev.Revision, "closed",
                        $"Decision {closure.DecisionId}: {closure.Rationale}", closure.ClosedBy, closure.ClosedAt), order));
                    break;
                case FindingReopenedV2 ev:
                    closure = null;
                    history.Add((new FindingHistoryEntryView(ev.Revision, "reopened", ev.Reason, ev.ReopenedBy,
                        ev.ReopenedAt), order));
                    break;
            }

            order++;
        }

        foreach (var change in actionChanges)
        {
            var position = history.FindLastIndex(item => item.Entry.At <= change.At) + 1;
            history.Insert(position, (change, -1));
        }
        foreach (var (completed, _) in completions)
        {
            // A completion belongs after the last finding fact that did not happen after it.
            var at = completed.CompletedAt ?? completed.CancelledAt!.Value;
            var position = history.FindLastIndex(item => item.Entry.At <= at) + 1;
            var change = completed.Status == FindingStates.Cancelled
                ? "corrective_action_cancelled" : "corrective_action_completed";
            history.Insert(position, (new FindingHistoryEntryView(completed.Revision, change,
                completed.ResolutionNotes ?? completed.CancellationReason!, completed.CompletedBy ?? completed.CancelledBy!, at), -1));
        }

        var view = new FindingView(tenant, raised.ProgramId, raised.FindingId, finding.Revision, raised.Source,
            raised.Title, raised.Description, finding.Severity!, finding.AffectedScope!, finding.OwnerMemberId,
            finding.DueOn, finding.RootCause, FindingStates.Open, FindingStates.Unresolved, links, actions, acceptances,
            closure, history.Select(static item => item.Entry).ToArray(), raised.RaisedBy, raised.RaisedAt,
            finding.ClosureProposal is { } proposal ? new FindingClosureProposalView(proposal.ClosureId, proposal.ProposalRevision,
                proposal.Terms, source.Admitted.Select(static item => item.Operation.LogicalId).ToArray(),
                proposal.ProposedBy, proposal.ProposedAt) : null, finding.Analysis, finding.AffectedRecords);
        return FindingViews.Settle(view, now);
    }

    /// <summary>The view of one finding at <paramref name="now" />, or null when it was not raised.</summary>
    public async ValueTask<FindingView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid findingId, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId), ct)
            .ConfigureAwait(false);
        return await ComposeAsync(finding, now, ct).ConfigureAwait(false);
    }

    /// <summary>The view of one finding, or a not-found failure.</summary>
    public async ValueTask<Result<FindingView>> ReadOneAsync(Uuid tenantId, Uuid programId, Uuid findingId,
        DateTimeOffset now, CancellationToken ct = default)
    {
        try
        {
            return await ReadViewAsync(tenantId, programId, findingId, now, ct).ConfigureAwait(false) is { } view
                ? Result<FindingView>.Success(view)
                : Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound, "The finding was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The finding content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }

    /// <summary>Reads every finding of the program with the closures each recorded.</summary>
    public async ValueTask<Result<FindingReadSnapshot>> ReadAsync(Uuid tenantId, Uuid programId, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var ids = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())));
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           FindingAggregate.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is FindingRaisedV2 raised &&
                raised.TenantId == tenantId && raised.ProgramId == programId)
                ids.Add(raised.FindingId);

        var views = new List<FindingView>();
        var closures = new Dictionary<Uuid, IReadOnlyList<FindingClosureView>>();
        try
        {
            foreach (var id in ids)
            {
                var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, id), ct)
                    .ConfigureAwait(false);
                if (await ComposeAsync(finding, now, ct).ConfigureAwait(false) is not { } view)
                    continue;
                views.Add(view);
                var history = new List<FindingClosureView>();
                var source = await FindingHistory.ReadAsync(events, finding, ct).ConfigureAwait(false);
                foreach (var closed in source.ClosureHistory)
                    history.Add(await FindingContent.ReadAsync<FindingClosureView>(content, tenantId,
                        closed.ClosureContent, ct).ConfigureAwait(false));
                closures.Add(id, history);
            }
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<FindingReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The finding content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        return Result<FindingReadSnapshot>.Success(new FindingReadSnapshot(Array.AsReadOnly(views
            .OrderBy(static finding => finding.RaisedAt)
            .ThenBy(static finding => finding.FindingId.ToString(), StringComparer.Ordinal).ToArray()), closures));
    }

    /// <summary>The authored action of an admission, with the admission header the materialized action copies.</summary>
    public async ValueTask<(ChildAdmissionIntent Intent, AddCorrectiveAction Authored)> ReadAdmissionAsync(Uuid tenantId,
        CorrectiveActionAdmitted admitted, CancellationToken ct = default)
    {
        var intent = await FindingContent.ReadAsync<ChildAdmissionIntent>(content, tenantId, admitted.Intent, ct)
            .ConfigureAwait(false);
        var authored = await FindingContent.ReadAsync<AddCorrectiveAction>(content, tenantId,
            intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        return (intent, authored);
    }

    async ValueTask<(CorrectiveActionView View, CorrectiveActionView? Completion, string AddedDescription)> ComposeActionAsync(
        FindingAggregate finding, CorrectiveActionAdmitted admitted, List<FindingHistoryEntryView> changes,
        CancellationToken ct)
    {
        var raised = finding.Raised!;
        var (intent, authored) = await ReadAdmissionAsync(raised.TenantId, admitted, ct).ConfigureAwait(false);
        var actionId = admitted.Operation.LogicalId;
        var addedDescription = authored.Description.Trim();
        var open = new CorrectiveActionView(actionId, addedDescription, authored.OwnerMemberId, authored.DueOn,
            FindingStates.Open, intent.Header.OriginalActor, intent.Header.AcceptedAt);
        var action = await reader.HydrateAsync(new CorrectiveAction(raised.TenantId, raised.ProgramId,
            raised.FindingId, actionId), ct).ConfigureAwait(false);
        if (!action.IsMaterialized)
            return (open, null, addedDescription);
        open = open with { Revision = action.Revision };
        if (action.Changed is { } changed)
        {
            open = open with { Description = changed.Description, OwnerMemberId = changed.OwnerMemberId, DueOn = changed.DueOn };
            var history = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, action, ct).ConfigureAwait(false);
            foreach (var fact in history.OfType<CorrectiveActionChanged>())
                changes.Add(new FindingHistoryEntryView(fact.Revision, "corrective_action_changed",
                    $"Action {fact.ActionId}: owner {fact.OwnerMemberId}, due {FindingViews.Date(fact.DueOn)}: {fact.Description}",
                    fact.ChangedBy, fact.ChangedAt));
        }
        if (action.Cancelled is { } cancelled)
        {
            var resolved = open with
            {
                Status = FindingStates.Cancelled,
                CancellationReason = cancelled.Reason,
                CancelledByMemberId = cancelled.CancelledByMemberId,
                CancelledBy = cancelled.CancelledBy,
                CancelledAt = cancelled.CancelledAt,
                Revision = cancelled.Revision,
            };
            return (resolved, resolved, addedDescription);
        }
        if (action.Completed is not { } completed)
            return (open, null, addedDescription);
        var evidence = await FindingContent.ReadAsync<IReadOnlyList<Operations.Models.EvidenceReference>>(content,
            raised.TenantId, completed.EvidenceManifest, ct).ConfigureAwait(false);
        var done = open with
        {
            Status = FindingStates.Completed,
            ResolutionNotes = completed.ResolutionNotes,
            Evidence = evidence,
            CompletedByMemberId = completed.CompletedByMemberId,
            CompletedBy = completed.CompletedBy,
            CompletedAt = completed.CompletedAt,
            Revision = completed.Revision,
        };
        return (done, done, addedDescription);
    }
}
