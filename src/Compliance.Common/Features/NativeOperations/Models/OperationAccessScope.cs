using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Server-created closed access scope keyed by canonical command kind.</summary>
public sealed record OperationAccessScope(string Realm, string PolicyKey, ActorReference OriginalActor,
    Uuid? TenantId, Uuid? ProgramId, EventStreamAddress AffectedOwner, ImmutableContentRef? TypedVisibilitySelector);
