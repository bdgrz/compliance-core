using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Server-created closed access scope keyed by canonical command kind.</summary>
public sealed record OperationAccessScope(string Realm, string PolicyKey, ActorReference OriginalActor,
    Uuid? TenantId, Uuid? ProgramId, EventStreamAddress AffectedOwner, ImmutableContentRef? TypedVisibilitySelector);
