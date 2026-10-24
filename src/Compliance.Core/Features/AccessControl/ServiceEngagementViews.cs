using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Authoritative reads of service engagement drafts. One engagement is read from its own owner; the list of a
///     client scans the facts of that client's engagement streams, so no cumulative parent view or cap exists. The
///     view of a revision is the retained result of the change that produced it.
/// </summary>
public sealed class ServiceEngagementViews(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>The current draft view of one engagement, or null when the engagement does not exist.</summary>
    public async ValueTask<ServiceEngagementView?> GetAsync(Uuid tenantId, Uuid engagementId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return null;
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        return owner.LatestResult is { } latest
            ? await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content,
                IndependenceContent.Realm(tenantId), latest, ct).ConfigureAwait(false)
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

    /// <summary>The current draft view of every engagement of a client, ordered by engagement identity.</summary>
    public async ValueTask<IReadOnlyList<ServiceEngagementView>> ListAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty)
            return [];
        var latest = new Dictionary<Uuid, ImmutableContentRef>();
        var pattern = EventStreamPattern.ForPattern(IndependenceContent.Realm(tenantId),
            ServiceEngagementIndependence.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not ServiceEngagementMutationRecorded change ||
                change.TenantId != tenantId || change.Operation.Owner != record.Stream)
                continue;
            latest[Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture)] =
                change.OperationResult!;
        }

        var realm = IndependenceContent.Realm(tenantId);
        var views = new List<ServiceEngagementView>(latest.Count);
        foreach (var result in latest.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            views.Add(await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm, result.Value, ct)
                .ConfigureAwait(false));
        return Array.AsReadOnly(views.ToArray());
    }
}
