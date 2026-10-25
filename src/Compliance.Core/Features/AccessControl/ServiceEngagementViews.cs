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
        var snapshot = await ServiceEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var history = new List<ServiceEngagementView>(snapshot.Changes.Count);
        foreach (var change in snapshot.Changes)
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
        var identities = new HashSet<Uuid>();
        var realm = IndependenceContent.Realm(tenantId);
        var pattern = EventStreamPattern.ForPattern(realm, ServiceEngagementIndependence.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Stream.Realm != realm || record.Stream.Area != ServiceEngagementIndependence.Area ||
                !Uuid.TryParse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture, out var engagementId) ||
                engagementId == Uuid.Empty)
                throw new ImmutableContentIntegrityException("Engagement discovery returned another owner scope.");
            identities.Add(engagementId);
        }

        var views = new List<ServiceEngagementView>(identities.Count);
        foreach (var engagementId in identities.OrderBy(id => id.ToString(), StringComparer.Ordinal))
            if (await GetAsync(tenantId, engagementId, ct).ConfigureAwait(false) is { } view)
                views.Add(view);
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
            var snapshot = await ServiceEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
            var denied = new List<(Uuid StaffMemberId, Uuid UserId)>(snapshot.DirectoryReceipts.Count);
            foreach (var receipt in snapshot.DirectoryReceipts)
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
