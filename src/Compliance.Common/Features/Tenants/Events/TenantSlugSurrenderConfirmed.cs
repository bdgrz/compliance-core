using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     The slug owner released the slug the tenant was surrendering.
/// </summary>
[Discriminator("bdgrz.tenant-slug-surrender.confirmed", 1)]
public sealed record TenantSlugSurrenderConfirmed(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
