using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     The tenant gave up the slug and the slug is retired for good.
/// </summary>
[Discriminator("bdgrz.tenant-slug.surrendered", 1)]
public sealed record TenantSlugSurrendered(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
