using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Values frozen at first native admission; child effects copy this header unchanged.</summary>
public sealed record DurableIntentHeader(Uuid RootOperationId, EventStreamAddress FirstOwner,
    EventStreamAddress AffectedOwner, EventStreamAddress ExpectedRevisionOwner, ActorReference OriginalActor,
    string Episode, ulong? OriginalExpectedRevision, string AuthoredIntentSha256, DateTimeOffset AcceptedAt);
