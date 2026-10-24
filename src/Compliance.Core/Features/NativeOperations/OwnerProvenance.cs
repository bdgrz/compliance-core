using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Builds and checks the provenance of operations whose first, affected and revision owner is one stream.</summary>
public static class OwnerProvenance
{
    /// <summary>The provenance of a root operation on <paramref name="owner" />, which is its own first, affected and revision scope.</summary>
    public static OperationProvenance For(Aggregate owner, Uuid commandId, string commandKind, ActorReference actor,
        string episode, ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(actor);
        return new OperationProvenance(commandId, commandId, null, commandKind, actor, owner.Stream, episode,
            originalExpectedRevision, authoredIntentSha256, acceptedAt, owner.Stream, owner.Stream);
    }

    /// <summary>
    ///     Rejects a native fact that does not accept its own logical operation on <paramref name="owner" />; a
    ///     reducer calls this so a malformed stream fails closed instead of binding the wrong operation.
    /// </summary>
    public static void RequireOwn(Aggregate owner, Uuid commandId, OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(operation);
        if (commandId == Uuid.Empty || operation.LogicalId != commandId || operation.Owner != owner.Stream)
            throw new InvalidOperationException(
                $"A {owner.GetType().Name} fact must accept its own logical operation on this owner.");
    }
}
