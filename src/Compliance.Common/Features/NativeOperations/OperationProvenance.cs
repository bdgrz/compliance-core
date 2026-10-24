using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Frozen acceptance provenance of one logical operation on its emitting native owner.</summary>
public sealed record OperationProvenance(Uuid LogicalId, Uuid RootOperationId, Uuid? CausationEventId,
    string CommandKind, ActorReference Actor, EventStreamAddress Owner, string Episode,
    ulong? OriginalExpectedRevision, string AuthoredIntentSha256, DateTimeOffset AcceptedAt,
    EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner);
