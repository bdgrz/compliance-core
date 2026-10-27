using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Frozen acceptance provenance of one logical operation on its emitting native owner.</summary>
public sealed record OperationProvenance(Uuid LogicalId, Uuid RootOperationId, Uuid? CausationEventId,
    string CommandKind, ActorReference Actor, EventStreamAddress Owner, string Episode,
    ulong? OriginalExpectedRevision, string AuthoredIntentSha256, DateTimeOffset AcceptedAt,
    EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner,
    EventStreamAddress? FirstOwner = null);
