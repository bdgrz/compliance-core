using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The tenant gave up the slug and the slug is retired for good.
/// </summary>
[Discriminator("bdgrz.tenant-slug.surrendered", 1)]
public sealed record TenantSlugSurrendered(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
