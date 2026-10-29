using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Content.Models;

/// <summary>The authority scope authenticated with every encrypted original response.</summary>
public sealed record ProtectedResponseScope(string Realm, EventStreamAddress AffectedOwner,
    Uuid RootOperationId, ActorReference OriginalActor);
