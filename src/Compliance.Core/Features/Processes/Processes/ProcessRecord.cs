using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

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
        var canonical = targets.Distinct(StringComparer.Ordinal).Order(Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance).ToArray();
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
