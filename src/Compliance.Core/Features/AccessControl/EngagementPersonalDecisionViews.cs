using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Authoritative reads of the independent personal decisions of an engagement. Every decision is its own
///     owner, so the decisions of one engagement are found by scanning the facts of the client's decision streams;
///     the retained content of each fact is the complete original view.
/// </summary>
public sealed class EngagementPersonalDecisionViews(IDomainEventReader events, IImmutableContentStore content)
{
    /// <summary>Every materialized management acknowledgement of one engagement, oldest first.</summary>
    public async ValueTask<IReadOnlyList<EngagementManagementAcknowledgementView>> ManagementAcknowledgementsAsync(
        Uuid tenantId, Uuid engagementId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return [];
        var realm = IndependenceContent.Realm(tenantId);
        var found = new List<EngagementManagementAcknowledgementView>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(realm,
                           EngagementManagementAcknowledgement.Area), EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not EngagementManagementAcknowledged acknowledged ||
                acknowledged.TenantId != tenantId || acknowledged.Operation.Owner != record.Stream)
                continue;
            var view = await content.ReadJsonAsync<EngagementManagementAcknowledgementView>(realm,
                acknowledged.AcknowledgementContent, ct).ConfigureAwait(false);
            if (view.EngagementId == engagementId)
                found.Add(view);
        }

        return Array.AsReadOnly(found.OrderBy(view => view.RecordedAt)
            .ThenBy(view => view.AcknowledgementId.ToString(), StringComparer.Ordinal).ToArray());
    }

    /// <summary>Every materialized partner evaluation of one engagement, oldest first.</summary>
    public async ValueTask<IReadOnlyList<PartnerIndependenceEvaluationView>> PartnerEvaluationsAsync(Uuid tenantId,
        Uuid engagementId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return [];
        var realm = IndependenceContent.Realm(tenantId);
        var found = new List<PartnerIndependenceEvaluationView>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(realm,
                           PartnerIndependenceDecision.Area), EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not PartnerIndependenceEvaluationRecorded recorded ||
                recorded.Operation.Owner != record.Stream)
                continue;
            var view = await content.ReadJsonAsync<PartnerIndependenceEvaluationView>(realm,
                recorded.EvaluationContent, ct).ConfigureAwait(false);
            if (view.EngagementId == engagementId && view.TenantId == tenantId)
                found.Add(view);
        }

        return Array.AsReadOnly(found.OrderBy(view => view.RecordedAt)
            .ThenBy(view => view.EvaluationId.ToString(), StringComparer.Ordinal).ToArray());
    }
}
