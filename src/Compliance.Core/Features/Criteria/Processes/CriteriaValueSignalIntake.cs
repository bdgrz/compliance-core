using System.Globalization;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Persists an exact-source signal before its reactor checkpoint may advance.</summary>
public sealed class CriteriaValueSignalIntake(IDomainEventReader events, IImmutableContentStore content,
    IProcessRecordStore records, TimeProvider clock)
{
    internal async ValueTask<Result> AcceptAsync(EventStreamAddress stream, ulong position, DomainEvent trigger,
        CancellationToken ct)
    {
        if (!CriteriaValueSignal.TryAddress(trigger, out var expected, out var aggregateId) || stream != expected ||
            trigger.Metadata.IsAudit || trigger.Metadata.EventId == Uuid.Empty ||
            trigger.Metadata.AggregateId != aggregateId || trigger.Metadata.AggregateVersion == 0)
            return Failure(RequestErrorKind.Validation, "The value proposal signal has an unexpected source identity.", false);

        await using var reader = events.ReadAsync(stream, position, ct).GetAsyncEnumerator(ct);
        if (!await reader.MoveNextAsync().ConfigureAwait(false))
            return Failure(RequestErrorKind.Conflict, "The exact value proposal source event is not yet observable.", true);
        var committed = reader.Current;
        if (committed.Stream != stream || committed.ResourceOffset != position || committed.Event != trigger ||
            committed.Event.Metadata.EventId != trigger.Metadata.EventId ||
            committed.Event.Metadata.AggregateId != aggregateId || committed.Event.Metadata.IsAudit ||
            committed.Event.Metadata.AggregateVersion != trigger.Metadata.AggregateVersion)
            return Failure(RequestErrorKind.Validation, "The value proposal signal differs from the committed source event.", false);

        var bytes = CriteriaValueSignal.Payload(stream, committed.Event, position);
        var intentId = AggregateIdentity.Create("CriteriaValueSignalIntent", stream.ToString(), trigger.Metadata.EventId);
        var intent = await content.WriteVerifiedAsync(stream.Realm, intentId, CriteriaValueSignal.Schema,
            "application/octet-stream", bytes, ct).ConfigureAwait(false);
        var reference = NativeFactReference.Of(stream, committed.Event);
        var key = ProcessKey.ForRoot(CriteriaValueSignal.ProcessName, trigger.Metadata.EventId, reference);
        var desired = ProcessRecord.Start(key, reference, CriteriaValueSignal.Episode, intent,
            [position.ToString(CultureInfo.InvariantCulture)], clock.GetUtcNow());
        var stored = await records.CreateIfAbsentAsync(desired, ct).ConfigureAwait(false);
        if (stored.Key != key || stored.Source != reference || stored.Episode != desired.Episode ||
            stored.Intent != intent || !stored.Targets.SequenceEqual(desired.Targets, StringComparer.Ordinal))
            return Failure(RequestErrorKind.Validation, "The signal identity is bound to different immutable intent.", false);
        // Successful create or an exact stored record is durable. Processing is deliberately deferred to ProcessSweep.
        return Result.Success;
    }

    static Result Failure(RequestErrorKind kind, string message, bool transient) =>
        Result.Failure(new RequestError(kind, message, transient));
}
