using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     The slug owner refused the tenant's surrender, so the tenant keeps the slug.
/// </summary>
[Discriminator("bdgrz.tenant-slug-surrender.failed", 1)]
public sealed record TenantSlugSurrenderFailed(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
