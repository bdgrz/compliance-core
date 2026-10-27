using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Resolves a trusted receiver's accepted native result from exact scoped source facts before content reads.</summary>
static class CorrectiveActionMaterializationReplay
{
    public static async ValueTask<Result<ReceiverOutcome>?> ResolveAsync(IDomainEventReader events,
        MaterializeCorrectiveAction command, Uuid tenant, CancellationToken ct)
    {
        if (command.Target.Area != CorrectiveAction.Area ||
            !Uuid.TryParse(command.Target.Resource, CultureInfo.InvariantCulture, out var identity))
            return SourceUseReceiverSupport.Permanent("The materialization target is not an exact corrective action owner.");
        CorrectiveActionAddedV2? materialized = null;
        DomainEvent? selected = null;
        ulong position = 0;
        ulong version = 0;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(command.Target, 0, ct).ConfigureAwait(false))
        {
            var fact = record.Event;
            if (record.Stream != command.Target || record.ResourceOffset != position++ || fact.Metadata.IsAudit ||
                fact.Metadata.AggregateId != identity || fact.Metadata.AggregateVersion != ++version ||
                fact.Metadata.EventId == Uuid.Empty || !ids.Add(fact.Metadata.EventId) ||
                fact is not INativeOperationFact native || native.Operation.Owner != command.Target ||
                native.Operation.LogicalId == Uuid.Empty || native.Operation.RootOperationId == Uuid.Empty)
                throw new ImmutableContentIntegrityException("The materialization replay history has another exact native source cut.");
            if (fact is CorrectiveActionAddedV2 added)
            {
                if (materialized is not null)
                    throw new ImmutableContentIntegrityException("An action has more than one native materialization.");
                materialized = added;
            }
            if (native.Operation.LogicalId != command.CommandId)
                continue;
            if (selected is not null)
                throw new ImmutableContentIntegrityException("A materialization receiver command is accepted more than once.");
            selected = fact;
        }
        if (materialized is null)
            return null;
        if (selected is not CorrectiveActionAddedV2 accepted || accepted != materialized ||
            accepted.TenantId != tenant || accepted.ProgramId == Uuid.Empty || accepted.FindingId != command.FindingId ||
            accepted.Revision != 1 || CorrectiveAction.IdentityOf(tenant, accepted.ProgramId, command.FindingId,
                command.ActionId) != identity || accepted.Intent != command.Intent)
            return SourceUseReceiverSupport.Permanent("The action was materialized by another exact operation or intent.");
        var expectedParent = new FindingAggregate(tenant, accepted.ProgramId, command.FindingId).Stream;
        var operation = accepted.Operation;
        if (command.Source.Stream != expectedParent || operation.CommandKind != nameof(MaterializeCorrectiveAction) ||
            operation.RootOperationId != command.OperationId || operation.CausationEventId != command.Source.EventId ||
            (operation.FirstOwner ?? operation.Owner) != expectedParent || operation.AffectedOwner != command.Target ||
            operation.ExpectedRevisionOwner != command.Target)
            return SourceUseReceiverSupport.Permanent("The accepted materialization belongs to another source or first owner.");
        // The command's source version/generation are verified against the original native admission, never guessed
        // from aggregate version minus one or reconstructed from unavailable immutable bodies.
        await foreach (var record in events.ReadAsync(expectedParent, 0, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != command.Source.EventId)
                continue;
            if (record.Stream != expectedParent || record.Event.Metadata.IsAudit ||
                record.Event.Metadata.AggregateId != new FindingAggregate(tenant, accepted.ProgramId, command.FindingId).Id ||
                record.Event.Metadata.AggregateVersion != command.Source.AggregateVersion ||
                record.Event is not CorrectiveActionAdmitted admission || admission.Owner != expectedParent ||
                admission.Generation != command.Generation || admission.Intent != command.Intent ||
                admission.Operation.Owner != expectedParent || admission.Operation.RootOperationId != command.OperationId ||
                admission.Operation.LogicalId != command.ActionId || operation.Actor != admission.Operation.Actor ||
                operation.Episode != admission.Operation.Episode ||
                operation.OriginalExpectedRevision != admission.Operation.OriginalExpectedRevision ||
                operation.AuthoredIntentSha256 != admission.Operation.AuthoredIntentSha256)
                return SourceUseReceiverSupport.Permanent("The replay source differs from the exact original native admission.");
            return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(command.Target, accepted));
        }
        return SourceUseReceiverSupport.Transient("The original materialization admission source is not visible yet.");
    }
}
