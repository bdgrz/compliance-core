using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>The authority scope authenticated with every encrypted original response.</summary>
public sealed record ProtectedResponseScope(string Realm, EventStreamAddress AffectedOwner,
    Uuid RootOperationId, ActorReference OriginalActor);
