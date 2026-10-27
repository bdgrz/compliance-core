using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     A tenant asked to change its slug. The fact is the native acceptance of the logical change on the tenant stream.
/// </summary>
[Discriminator("bdgrz.tenant-slug.change-requested", 1)]
public sealed record TenantSlugChangeRequested(Uuid TenantId, string OldSlug, string NewSlug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact
{
    /// <summary>The caller who asked for the change, when known.</summary>
    public ActorReference? RequestedBy { get; init; }

    /// <summary>When the caller asked for the change, when known.</summary>
    public DateTimeOffset? RequestedAt { get; init; }
}
