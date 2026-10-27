using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     The slug owner registered the tenant's first slug and the tenant confirmed it.
/// </summary>
[Discriminator("bdgrz.tenant-slug.confirmed", 1)]
public sealed record TenantSlugConfirmed(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
