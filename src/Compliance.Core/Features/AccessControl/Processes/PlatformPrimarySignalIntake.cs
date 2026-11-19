using System.Globalization;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>Persists one exact primary participant attempt before its signal checkpoint advances.</summary>
public sealed class PlatformPrimarySignalIntake(IDomainEventReader events, IAggregateReader aggregates,
    IImmutableContentStore content, IProcessRecordStore records, TimeProvider clock)
{
    internal const string ProcessName = "platform-primary-participant-v1";
    internal const string Schema = "platform-primary-participant.v1";

    internal async ValueTask<Result> AcceptAsync(EventStreamAddress stream, ulong position, DomainEvent signal,
        CancellationToken ct)
    {
        if (stream != PrimaryPlatformOperators.Address || signal.Metadata.AggregateId != PrimaryPlatformOperators.OwnerId ||
            signal.Metadata.IsAudit || signal.Metadata.EventId == Uuid.Empty || !TryTarget(signal, out var user, out var operation))
            return Invalid();
        await using var source = events.ReadAsync(stream, position, ct).GetAsyncEnumerator(ct);
        if (!await source.MoveNextAsync().ConfigureAwait(false))
            return Pending();
        if (source.Current.Stream != stream || source.Current.ResourceOffset != position ||
            source.Current.Event.Metadata.EventId != signal.Metadata.EventId || source.Current.Event != signal)
            return Invalid();
        var reference = NativeFactReference.Of(stream, signal);
        var key = ProcessKey.ForRoot(ProcessName, signal.Metadata.EventId, reference);
        var bytes = Payload(reference, user, operation);
        var intentId = AggregateIdentity.Create("PlatformPrimarySignalIntent", stream.ToString(), signal.Metadata.EventId);
        var intent = await content.WriteVerifiedAsync(stream.Realm, intentId, Schema, "application/octet-stream", bytes, ct)
            .ConfigureAwait(false);
        var stored = await records.ReadAsync(key, ct).ConfigureAwait(false);
        if (stored is not null)
            return Matches(stored, key, reference, intent, position) ? Result.Success : Invalid();

        var target = await aggregates.HydrateAsync(new PlatformOperator(user, operation), ct).ConfigureAwait(false);
        // Existing selected outcomes must be recovered before choosing a new target head.
        if (target.SelectedOutcome is not null)
            return Pending();
        var slot = string.Create(CultureInfo.InvariantCulture, $"{position}:{target.CommittedStreamPosition}:{target.Version}");
        var desired = ProcessRecord.Start(key, reference, ProcessName, intent, [slot], clock.GetUtcNow());
        stored = await records.CreateIfAbsentAsync(desired, ct).ConfigureAwait(false);
        return Matches(stored, key, reference, intent, position) ? Result.Success : Invalid();
    }

    internal static bool TryTarget(DomainEvent signal, out Uuid user, out Uuid operation)
    {
        (user, operation) = signal switch
        {
            PrimaryPlatformOperatorSeatReserved x => (x.UserId, x.OperationId),
            PrimaryPlatformOperatorSeatReleased x => (x.UserId, x.OperationId),
            _ => (Uuid.Empty, Uuid.Empty),
        };
        return user != Uuid.Empty && operation != Uuid.Empty;
    }

    internal static byte[] Payload(NativeFactReference source, Uuid user, Uuid operation) => Encoding.UTF8.GetBytes(
        string.Join('\n', Schema, source.Stream.ToString(), source.EventId.ToString(),
            source.AggregateVersion.ToString(CultureInfo.InvariantCulture), PlatformOperator.AddressOf(user).ToString(), operation.ToString()));

    internal static bool TrySlot(string value, out ulong source, out ulong target, out ulong revision)
    {
        source = target = revision = 0;
        var parts = value.Split(':');
        return parts.Length == 3 && ulong.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out source) &&
            ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out target) &&
            ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out revision);
    }

    static bool Matches(ProcessRecord record, ProcessKey key, NativeFactReference source, ImmutableContentRef intent, ulong position) =>
        record.Key == key && record.Source == source && record.Episode == ProcessName && record.Intent == intent &&
        record.Targets.Count == 1 && TrySlot(record.Targets[0], out var saved, out _, out _) && saved == position;
    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The primary participant attempt is pending.", true));
    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation, "The primary participant signal has another intent."));
}
