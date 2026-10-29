using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

/// <summary>Resolves a trusted receiver's original exact native result before reading mutable or unavailable bodies.</summary>
static class AuditorChildMaterializationReplay
{
    public static async ValueTask<Result<ReceiverOutcome>?> ResolveAsync(IDomainEventReader events,
        NativeFactReference source, EventStreamAddress target, Uuid tenant, Uuid requestId, Uuid childId,
        Uuid commandId, Uuid operationId, long generation, ImmutableContentRef intent, AdmittedRequestChildKind kind,
        CancellationToken ct)
    {
        var area = kind switch
        {
            AdmittedRequestChildKind.Response => AuditorResponse.Area,
            AdmittedRequestChildKind.Comment => AuditorFeedbackThread.Area,
            AdmittedRequestChildKind.SampleSelection => AuditorSampleSelection.Area,
            _ => string.Empty,
        };
        if (target.Area != area || target.Realm != tenant.ToString() ||
            !Uuid.TryParse(target.Resource, CultureInfo.InvariantCulture, out var identity))
            return SourceUseReceiverSupport.Permanent("The materialization target is not an exact scoped child owner.");
        DomainEvent? created = null;
        DomainEvent? selected = null;
        ulong position = 0;
        ulong version = 0;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(target, 0, ct).ConfigureAwait(false))
        {
            var fact = record.Event;
            if (record.Stream != target || record.ResourceOffset != position++ || fact.Metadata.AggregateId != identity ||
                fact.Metadata.EventId == Uuid.Empty || !ids.Add(fact.Metadata.EventId))
                throw Invalid("The child replay history has another exact physical source prefix.");
            if (fact.Metadata.IsAudit)
                continue;
            if (fact.Metadata.AggregateVersion != ++version || fact is not INativeOperationFact native ||
                native.Operation.Owner != target)
                throw Invalid("The child replay history has another exact native source cut.");
            if (fact is AuditResponseDraftCreated or AuditorRequestCommentRecorded or SampleSelectionRecorded)
            {
                if (created is not null)
                    throw Invalid("A child has multiple native materializations.");
                created = fact;
            }
            if (native.Operation.LogicalId != commandId)
                continue;
            if (selected is not null)
                throw Invalid("A child receiver command is accepted more than once.");
            selected = fact;
        }
        if (created is null)
            return null;
        var valid = selected is not null && ReferenceEquals(selected, created) && (selected switch
        {
            AuditResponseDraftCreated response => kind == AdmittedRequestChildKind.Response &&
                response.TenantId == tenant && response.SubjectId == childId && response.Revision == 1 && response.Change == intent,
            AuditorRequestCommentRecorded comment => kind == AdmittedRequestChildKind.Comment &&
                comment.TenantId == tenant && comment.SubjectId == childId && comment.Revision == 1 && comment.Change == intent,
            SampleSelectionRecorded selection => kind == AdmittedRequestChildKind.SampleSelection &&
                selection.TenantId == tenant && selection.SubjectId == childId && selection.Revision == 1 && selection.Change == intent,
            _ => false,
        });
        if (!valid)
            return SourceUseReceiverSupport.Permanent("The child was materialized by another exact command or intent.");
        var operation = ((INativeOperationFact)selected!).Operation;
        var parent = new EventStreamAddress(tenant.ToString(), AuditorRequest.Area, requestId.ToString());
        var commandKind = kind switch
        {
            AdmittedRequestChildKind.Response => nameof(MaterializeAuditorResponse),
            AdmittedRequestChildKind.Comment => nameof(MaterializeAuditorComment),
            _ => nameof(MaterializeAuditorSampleSelection),
        };
        if (source.Stream != parent || operation.CommandKind != commandKind || operation.RootOperationId != operationId ||
            operation.CausationEventId != source.EventId || (operation.FirstOwner ?? operation.Owner) != parent ||
            operation.AffectedOwner != target || operation.ExpectedRevisionOwner != target)
            return SourceUseReceiverSupport.Permanent("The accepted child belongs to another exact admission or first owner.");
        await foreach (var record in events.ReadAsync(parent, 0, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != source.EventId)
                continue;
            var admission = record.Event;
            var matching = admission switch
            {
                AuditorResponseAdmitted response => kind == AdmittedRequestChildKind.Response && response.Owner == parent &&
                    response.Generation == generation && response.Intent == intent,
                AuditorCommentAdmitted comment => kind == AdmittedRequestChildKind.Comment && comment.Owner == parent &&
                    comment.Generation == generation && comment.Intent == intent,
                AuditorSampleSelectionAdmitted selection => kind == AdmittedRequestChildKind.SampleSelection && selection.Owner == parent &&
                    selection.Generation == generation && selection.Intent == intent,
                _ => false,
            };
            if (!matching || record.Stream != parent || admission.Metadata.IsAudit ||
                admission.Metadata.AggregateId != requestId || admission.Metadata.AggregateVersion != source.AggregateVersion ||
                admission is not INativeOperationFact native || native.Operation.LogicalId != childId ||
                native.Operation.RootOperationId != operationId || native.Operation.Owner != parent ||
                operation.Actor != native.Operation.Actor || operation.Episode != native.Operation.Episode ||
                operation.AuthoredIntentSha256 != native.Operation.AuthoredIntentSha256 ||
                operation.OriginalExpectedRevision != native.Operation.OriginalExpectedRevision)
                return SourceUseReceiverSupport.Permanent("The replay source differs from the exact original native admission.");
            return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(target, selected!));
        }
        return SourceUseReceiverSupport.Transient("The original admitted child source is not visible yet.");
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
