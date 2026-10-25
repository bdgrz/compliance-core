using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Resolves accepted terminal receiver commands from the exact first-owner stream before content reads.</summary>
static class AuditorResolutionReplay
{
    public static async Task<Result<ReceiverOutcome>?> ResolveAsync(IDomainEventReader events,
        AbortAuditorRequestResolution command, CancellationToken ct)
    {
        var (selected, admission) = await ReadAsync(events, command.Target, command.CommandId, command.OperationId, ct)
            .ConfigureAwait(false);
        if (selected is null)
            return null;
        if (selected is not AuditorRequestResolutionCanceled canceled || admission is null ||
            canceled.Generation != command.Generation || canceled.SourceProofs != command.FailureProof ||
            canceled.Intent != admission.Intent || canceled.Operation.CommandKind != nameof(AbortAuditorRequestResolution) ||
            canceled.Operation.CausationEventId != admission.Metadata.EventId || !Matches(canceled.Operation, admission))
            return SourceUseReceiverSupport.Permanent("The accepted cancellation belongs to another exact failure proof or admission.");
        return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(command.Target, selected));
    }

    public static async Task<Result<ReceiverOutcome>?> ResolveAsync(IDomainEventReader events,
        CompleteAuditorRequestResolution command, CancellationToken ct)
    {
        var (selected, admission) = await ReadAsync(events, command.Target, command.CommandId, command.OperationId, ct)
            .ConfigureAwait(false);
        if (selected is null)
            return null;
        if (selected is not AuditorRequestResolved resolved || admission is null ||
            command.Source != NativeFactReference.Of(command.Target, admission) || resolved.SubjectId != command.RequestId ||
            resolved.RequestGeneration != command.Generation || resolved.Change != command.Intent ||
            resolved.ResponseDispositionProofs != command.ResponseProofManifest ||
            resolved.Operation.CommandKind != nameof(CompleteAuditorRequestResolution) ||
            resolved.Operation.CausationEventId != admission.Metadata.EventId || !Matches(resolved.Operation, admission))
            return SourceUseReceiverSupport.Permanent("The accepted resolution belongs to another exact source, intent or disposition proof.");
        return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(command.Target, selected));
    }

    static async Task<(DomainEvent? Selected, AuditorRequestResolutionAccepted? Admission)> ReadAsync(
        IDomainEventReader events, EventStreamAddress stream, Uuid commandId, Uuid rootId, CancellationToken ct)
    {
        if (stream.Area != AuditorRequest.Area || !Uuid.TryParse(stream.Resource, CultureInfo.InvariantCulture, out var id))
            throw Invalid("The resolution receiver target is not an exact request owner.");
        DomainEvent? selected = null;
        AuditorRequestResolutionAccepted? accepted = null;
        ulong position = 0;
        ulong version = 0;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var fact = record.Event;
            if (record.Stream != stream || record.ResourceOffset != position++ || fact.Metadata.AggregateId != id ||
                fact.Metadata.EventId == Uuid.Empty || !ids.Add(fact.Metadata.EventId))
                throw Invalid("The receiver replay has another physical source prefix.");
            if (fact.Metadata.IsAudit)
                continue;
            if (fact.Metadata.AggregateVersion != ++version || fact is not INativeOperationFact native ||
                native.Operation.Owner != stream)
                throw Invalid("The receiver replay has another native source cut.");
            if (fact is AuditorRequestResolutionAccepted admission && admission.Operation.RootOperationId == rootId)
            {
                if (accepted is not null)
                    throw Invalid("A resolution root was admitted more than once.");
                accepted = admission;
            }
            if (native.Operation.LogicalId != commandId)
                continue;
            if (selected is not null)
                throw Invalid("A receiver command was accepted more than once.");
            selected = fact;
        }
        return (selected, accepted);
    }

    static bool Matches(OperationProvenance operation, AuditorRequestResolutionAccepted admission) =>
        operation.RootOperationId == admission.Operation.RootOperationId && operation.FirstOwner == admission.Owner &&
        operation.AffectedOwner == admission.Owner && operation.ExpectedRevisionOwner == admission.Owner &&
        operation.Actor == admission.Operation.Actor && operation.Episode == admission.Operation.Episode &&
        operation.AuthoredIntentSha256 == admission.Operation.AuthoredIntentSha256 &&
        operation.OriginalExpectedRevision == admission.Operation.OriginalExpectedRevision;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
