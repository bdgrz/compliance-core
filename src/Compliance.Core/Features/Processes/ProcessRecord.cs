using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
/// The durable CAS record of one ordered process. It freezes the accepted source fact, the immutable intent and the
/// canonical target list, and tracks the stage, receiver and target cursor, attempts, due time, lease and state.
/// <see cref="Version"/> is assigned by the store and must match on every replace.
/// </summary>
public sealed record ProcessRecord(ProcessKey Key, NativeFactReference Source, string Episode,
    ImmutableContentRef Intent, IReadOnlyList<string> Targets, int StageIndex, int ReceiverIndex, int TargetIndex,
    int AbortIndex, ProcessState State, int Attempts, DateTimeOffset NextDueAt, ProcessLease? Lease, string? Note,
    long Version)
{
    /// <summary>
    ///     The native fact each receiver of this record committed or replayed, by receiver name. A later receiver
    ///     builds its source proof from them, so a crash between receivers never loses what an earlier one produced.
    /// </summary>
    public IReadOnlyDictionary<string, NativeFactReference> Facts { get; init; } =
        new Dictionary<string, NativeFactReference>(StringComparer.Ordinal);

    /// <summary>
    ///     The key under which <see cref="Facts"/> keeps the fact a step produced: the receiver name, and for a step that
    ///     dispatched against one canonical target the receiver and that target, so each target keeps its own fact.
    /// </summary>
    public static string FactKey(string receiver, string? target) =>
        target is null ? receiver : $"{receiver}:{target}";

    /// <summary>A new pending record at the first stage, due immediately.</summary>
    public static ProcessRecord Start(ProcessKey key, NativeFactReference source, string episode,
        ImmutableContentRef intent, IEnumerable<string> targets, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(targets);
        // Keys are acquired in canonical unsigned UTF-8 order regardless of authored input order.
        var canonical = targets.Distinct(StringComparer.Ordinal).Order(Kernel.StreamKeyOrder.Instance).ToArray();
        return new ProcessRecord(key, source, episode, intent, canonical, 0, 0, 0, 0, ProcessState.Pending, 0, now,
            null, null, 0);
    }

    /// <summary>True once no further dispatch is possible.</summary>
    public bool IsTerminal => State is ProcessState.Completed or ProcessState.Aborted or ProcessState.Quarantined;

    /// <summary>Value equality; the target list compares by sequence so a stored record equals its round trip.</summary>
    public bool Equals(ProcessRecord? other) =>
        other is not null && Key == other.Key && Source == other.Source && Episode == other.Episode &&
        Intent == other.Intent && Targets.SequenceEqual(other.Targets, StringComparer.Ordinal) &&
        StageIndex == other.StageIndex && ReceiverIndex == other.ReceiverIndex && TargetIndex == other.TargetIndex &&
        AbortIndex == other.AbortIndex && State == other.State && Attempts == other.Attempts &&
        NextDueAt == other.NextDueAt && Lease == other.Lease && Note == other.Note && Version == other.Version &&
        Facts.Count == other.Facts.Count && Facts.All(pair => other.Facts.TryGetValue(pair.Key, out var fact) &&
                                                              fact == pair.Value);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Key, Source, State, Version);
}
