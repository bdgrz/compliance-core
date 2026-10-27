using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

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
                case CorrectiveActionAdmitted ev:
                    var action = await ComposeActionAsync(finding, ev, ct).ConfigureAwait(false);
                    actions.Add(action.View);
                    history.Add((new FindingHistoryEntryView(ev.Revision, "corrective_action_added",
                        action.View.Description, action.View.AddedBy, action.View.AddedAt), order));
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

        foreach (var (completed, _) in completions)
        {
            // A completion belongs after the last finding fact that did not happen after it.
            var at = completed.CompletedAt!.Value;
            var position = history.FindLastIndex(item => item.Entry.At <= at) + 1;
            history.Insert(position, (new FindingHistoryEntryView(completed.Revision, "corrective_action_completed",
                completed.ResolutionNotes!, completed.CompletedBy!, at), -1));
        }

        var view = new FindingView(tenant, raised.ProgramId, raised.FindingId, finding.Revision, raised.Source,
            raised.Title, raised.Description, finding.Severity!, finding.AffectedScope!, finding.OwnerMemberId,
            finding.DueOn, finding.RootCause, FindingStates.Open, FindingStates.Unresolved, links, actions, acceptances,
            closure, history.Select(static item => item.Entry).ToArray(), raised.RaisedBy, raised.RaisedAt);
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

    async ValueTask<(CorrectiveActionView View, CorrectiveActionView? Completion)> ComposeActionAsync(
        FindingAggregate finding, CorrectiveActionAdmitted admitted, CancellationToken ct)
    {
        var raised = finding.Raised!;
        var (intent, authored) = await ReadAdmissionAsync(raised.TenantId, admitted, ct).ConfigureAwait(false);
        var actionId = admitted.Operation.LogicalId;
        var open = new CorrectiveActionView(actionId, authored.Description.Trim(), authored.OwnerMemberId, authored.DueOn,
            FindingStates.Open, intent.Header.OriginalActor, intent.Header.AcceptedAt);
        var action = await reader.HydrateAsync(new CorrectiveAction(raised.TenantId, raised.ProgramId,
            raised.FindingId, actionId), ct).ConfigureAwait(false);
        if (!action.IsMaterialized)
            return (open, null);
        open = open with { Revision = action.Revision };
        if (action.Completed is not { } completed)
            return (open, null);
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
        return (done, done);
    }
}
