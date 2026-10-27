using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The slug owner released the slug the tenant was surrendering.
/// </summary>
[Discriminator("bdgrz.tenant-slug-surrender.confirmed", 1)]
public sealed record TenantSlugSurrenderConfirmed(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
