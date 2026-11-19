using System.Collections.ObjectModel;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
/// Captures one bounded set of exact source owners around a common instant.
/// A stable capture is not an authorization decision: a trusted caller must
/// evaluate the complete joined path, policy, expiry and every applicable veto.
/// </summary>
sealed class AccessAdmissionStableRead(AccessAdmissionStableRead.ISource source, TimeProvider clock)
{
    /// <summary>Fixed participant roles; none denotes a collection of children.</summary>
    internal enum Slot
    {
        Identity,
        Policy,
        Session,
        Operator,
        Tenant,
        Member,
        Primary,
        TeamMember,
        Team,
        TeamRole,
        Role,
        RolePermission,
        Account,
        GlobalVeto
    }

    /// <summary>One exact owner to observe for a fixed candidate path.</summary>
    internal sealed record Participant(Slot Role, string Stream);

    /// <summary>
    /// Immutable owner state at its exact non-resetting generation and physical
    /// position. The provider is responsible for binding the state to the version.
    /// </summary>
    internal sealed record Observation(AccessAdmissionSourceVersion Version, object State);

    /// <summary>
    /// Qualified source snapshots only. Ordinary event replay is not implicitly
    /// qualified as a linearizable, current-to-head read.
    /// </summary>
    internal interface ISource
    {
        /// <summary>Whether deployment and source guarantees have been qualified.</summary>
        bool IsFreshnessQualified { get; }

        /// <summary>
        /// Reads a complete immutable current state from the exact owner, or null
        /// if currentness, source generation, or complete retention is unknown.
        /// </summary>
        ValueTask<Observation?> ReadCurrentAsync(string stream, CancellationToken ct);
    }

    /// <summary>A common instant and the first-pass immutable source observations.</summary>
    internal sealed record Capture(DateTimeOffset AdmittedAt, IReadOnlyDictionary<Slot, Observation> Observations);

    /// <summary>
    /// Reads all selected owners, samples time, and rereads the same owners.
    /// Null means no coherent snapshot was established, not a definitive denial.
    /// </summary>
    internal async ValueTask<Capture?> CaptureAsync(IReadOnlyList<Participant> participants,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ct.ThrowIfCancellationRequested();
        if (!source.IsFreshnessQualified || !ValidPath(participants))
            return null;

        var first = new Dictionary<Slot, Observation>();
        foreach (var participant in participants)
        {
            var observation = await source.ReadCurrentAsync(participant.Stream, ct).ConfigureAwait(false);
            if (!ValidObservation(participant, observation))
                return null;
            first.Add(participant.Role, observation!);
        }

        var instant = clock.GetUtcNow().ToUniversalTime();
        foreach (var participant in participants)
        {
            var second = await source.ReadCurrentAsync(participant.Stream, ct).ConfigureAwait(false);
            if (!ValidObservation(participant, second) || second!.Version != first[participant.Role].Version)
                return null;
        }

        return source.IsFreshnessQualified
            ? new Capture(instant, new ReadOnlyDictionary<Slot, Observation>(first))
            : null;
    }

    static bool ValidPath(IReadOnlyList<Participant> participants)
    {
        var roles = new HashSet<Slot>();
        foreach (var participant in participants)
        {
            if (participant is null || !Enum.IsDefined(participant.Role) ||
                !roles.Add(participant.Role) || !CanonicalStream(participant.Stream))
                return false;
        }

        return roles.Contains(Slot.Identity) && roles.Contains(Slot.Policy);
    }

    static bool ValidObservation(Participant participant, Observation? observation) =>
        observation is not null && observation.State is not null && observation.Version is not null &&
        !string.IsNullOrWhiteSpace(observation.Version.Generation) &&
        string.Equals(observation.Version.Stream, participant.Stream, StringComparison.Ordinal);

    static bool CanonicalStream(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            return string.Equals(EventStreamAddress.Parse(value).ToString(), value, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
