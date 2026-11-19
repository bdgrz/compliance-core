using System.Globalization;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Schedules one proposal without waiting for its independent decision.</summary>
public sealed class CriteriaEntrySignalIntake(IImmutableContentStore content, IProcessRecordStore records, TimeProvider clock)
{
    internal async ValueTask<Result> AcceptAsync(EventStreamAddress stream, ulong position, DomainEvent trigger,
        CancellationToken ct)
    {
        if (!CriteriaEntrySignal.TryAddress(trigger, out var expected, out var aggregateId) || stream != expected ||
            trigger.Metadata.IsAudit || trigger.Metadata.EventId == Uuid.Empty ||
            trigger.Metadata.AggregateId != aggregateId || trigger.Metadata.AggregateVersion != checked(position + 1))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The entry signal has an unexpected source identity."));

        // Scheduling does not decide authority. The independent receiver re-reads
        // this exact source and verifies every participant before any target write.
        var bytes = CriteriaEntrySignal.Payload(stream, trigger, position);
        var intentId = AggregateIdentity.Create("CriteriaEntrySignalIntent", stream.ToString(), trigger.Metadata.EventId);
        var intent = await content.WriteVerifiedAsync(stream.Realm, intentId, CriteriaEntrySignal.Schema,
            "application/octet-stream", bytes, ct).ConfigureAwait(false);
        var reference = NativeFactReference.Of(stream, trigger);
        var key = ProcessKey.ForRoot(CriteriaEntrySignal.ProcessName, trigger.Metadata.EventId, reference);
        var desired = ProcessRecord.Start(key, reference, CriteriaEntrySignal.Episode, intent,
            [position.ToString(CultureInfo.InvariantCulture)], clock.GetUtcNow());
        var stored = await records.CreateIfAbsentAsync(desired, ct).ConfigureAwait(false);
        if (stored.Key != key || stored.Source != reference || stored.Episode != desired.Episode ||
            stored.Intent != intent || !stored.Targets.SequenceEqual(desired.Targets, StringComparer.Ordinal))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The signal identity has different immutable intent."));
        return Result.Success;
    }
}
