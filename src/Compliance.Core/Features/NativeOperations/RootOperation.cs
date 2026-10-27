using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Builds the provenance of a public command that is the root operation of its own first owner.</summary>
public static class RootOperation
{
    /// <summary>The episode of every first-owner operation of a public command.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>
    /// The provenance of a root operation whose first, affected and expected-revision owner is the same stream.
    /// <paramref name="expectedRevision"/> is null for a creation that has no caller revision.
    /// </summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, long? expectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(actor);
        if (expectedRevision is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "An expected revision cannot be negative.");
        return new OperationProvenance(logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode,
            expectedRevision is { } revision ? (ulong)revision : null, authoredIntentSha256, acceptedAt, owner, owner);
    }
}
