using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     The slug owner refused the tenant's first slug.
/// </summary>
[Discriminator("bdgrz.tenant-slug.rejected", 1)]
public sealed record TenantSlugRejected(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
