using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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
