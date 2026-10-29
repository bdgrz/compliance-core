using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>
/// The comparison fields of an accepted logical operation. Reuse of a logical id is an exact replay only when every
/// field is equal; a hash alone never stands in for the owner, actor or episode checks. Actor identity is kind and
/// subject; its server-enriched display snapshot remains on the original fact and does not change authored intent.
/// </summary>
public sealed record AcceptedOperationBinding(string Realm, Uuid LogicalId, string CommandKind,
    EventStreamAddress FirstOwner, EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner,
    string Episode, ActorReference OriginalActor, ulong? OriginalExpectedRevision, string AuthoredIntentSha256)
{
    /// <summary>Derives the binding carried by an accepted native fact.</summary>
    public static AcceptedOperationBinding Of(OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new AcceptedOperationBinding(operation.Owner.Realm, operation.LogicalId, operation.CommandKind,
            operation.Owner, operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Episode,
            operation.Actor with { Display = string.Empty }, operation.OriginalExpectedRevision,
            operation.AuthoredIntentSha256);
    }
}
