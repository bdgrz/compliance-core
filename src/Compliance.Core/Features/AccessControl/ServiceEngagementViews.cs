using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Authoritative reads of service engagements. One engagement is read from its own owner; the list of a client
///     scans the facts of that client's engagement streams, so no cumulative parent view or cap exists. The view of a
///     revision is the retained result of the change that produced it, and the current view carries the status and
///     revision the owner holds, so an accepted engagement reads as accepted and a closed one as closed.
/// </summary>
public sealed class ServiceEngagementViews(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>The current view of one engagement, or null when the engagement does not exist.</summary>
    public async ValueTask<ServiceEngagementView?> GetAsync(Uuid tenantId, Uuid engagementId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return null;
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        return owner.LatestResult is { } latest
            ? Overlay(await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content,
                IndependenceContent.Realm(tenantId), latest, ct).ConfigureAwait(false), owner.Status, owner.Revision)
            : null;
    }

    /// <summary>Every retained revision of one engagement in order, or null when the engagement does not exist.</summary>
    public async ValueTask<IReadOnlyList<ServiceEngagementView>?> HistoryAsync(Uuid tenantId, Uuid engagementId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return null;
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        if (!owner.Exists)
            return null;
        var realm = IndependenceContent.Realm(tenantId);
        var history = new List<ServiceEngagementView>(owner.Changes.Count);
        foreach (var change in owner.Changes)
            history.Add(await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm,
                change.OperationResult!, ct).ConfigureAwait(false));
        return Array.AsReadOnly(history.ToArray());
    }

    /// <summary>The current view of every engagement of a client, ordered by engagement identity.</summary>
    public async ValueTask<IReadOnlyList<ServiceEngagementView>> ListAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty)
            return [];
        var latest = new Dictionary<Uuid, (ImmutableContentRef Result, string Status, ulong Revision)>();
        var pattern = EventStreamPattern.ForPattern(IndependenceContent.Realm(tenantId),
            ServiceEngagementIndependence.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var engagementId = Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture);
            switch (record.Event)
            {
                case ServiceEngagementMutationRecorded change when change.TenantId == tenantId &&
                    change.Operation.Owner == record.Stream:
                    latest[engagementId] = (change.OperationResult!, change.Intent == ServiceEngagementIndependence.CloseKind
                        ? "closed"
                        : "draft", change.Revision);
                    break;
                case ServiceEngagementAcceptanceRecorded recorded when latest.TryGetValue(engagementId, out var current):
                    latest[engagementId] = (current.Result, "accepted", recorded.Revision);
                    break;
                case ServiceEngagementStaffAssigned assigned when latest.TryGetValue(engagementId, out var current):
                    latest[engagementId] = (current.Result, current.Status, assigned.Revision);
                    break;
                case ServiceEngagementAssignmentRevoked revoked when latest.TryGetValue(engagementId, out var current):
                    latest[engagementId] = (current.Result, revoked.StaffMemberId is null ? "closed" : current.Status,
                        revoked.Revision);
                    break;
                case DirectoryIndependenceReevaluated receipt when latest.TryGetValue(engagementId, out var current):
                    latest[engagementId] = (current.Result, current.Status, receipt.Revision);
                    break;
            }
        }

        var realm = IndependenceContent.Realm(tenantId);
        var views = new List<ServiceEngagementView>(latest.Count);
        foreach (var item in latest.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            views.Add(Overlay(await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm,
                item.Value.Result, ct).ConfigureAwait(false), item.Value.Status, item.Value.Revision));
        return Array.AsReadOnly(views.ToArray());
    }

    /// <summary>
    ///     Every accepted engagement of a client with its current accepted snapshot and the staff members that a
    ///     directory receipt denies, ordered by engagement identity. Each is read from its own owner.
    /// </summary>
    public async ValueTask<IReadOnlyList<AcceptedEngagement>> AcceptedAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty)
            return [];
        var accepted = new HashSet<Uuid>();
        var pattern = EventStreamPattern.ForPattern(IndependenceContent.Realm(tenantId),
            ServiceEngagementIndependence.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (!record.Event.Metadata.IsAudit && record.Event is ServiceEngagementAcceptanceRecorded)
                accepted.Add(Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture));
        }

        var realm = IndependenceContent.Realm(tenantId);
        var found = new List<AcceptedEngagement>(accepted.Count);
        foreach (var engagementId in accepted.OrderBy(id => id.ToString(), StringComparer.Ordinal))
        {
            var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
                .ConfigureAwait(false);
            if (owner.LatestResult is not { } latest || owner.AcceptanceResult is not { } result)
                continue;
            var denied = new List<(Uuid StaffMemberId, Uuid UserId)>(owner.DirectoryReceipts.Count);
            foreach (var receipt in owner.DirectoryReceipts)
            {
                var view = await content.ReadJsonAsync<DirectoryIndependenceReevaluationView>(realm,
                    receipt.ReevaluationContent, ct).ConfigureAwait(false);
                denied.Add((view.Source.StaffMemberId, view.Source.UserId));
            }

            found.Add(new AcceptedEngagement(
                Overlay(await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm, latest, ct)
                    .ConfigureAwait(false), owner.Status, owner.Revision),
                await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm, result, ct)
                    .ConfigureAwait(false), denied));
        }

        return Array.AsReadOnly(found.ToArray());
    }

    static ServiceEngagementView Overlay(ServiceEngagementView view, string status, ulong revision) =>
        status == "none" || status == view.Status && (long)revision == view.Revision
            ? view
            : view with { Status = status, Revision = (long)revision };
}
