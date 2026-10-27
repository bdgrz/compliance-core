using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     A tenant started surrendering its confirmed slug.
/// </summary>
[Discriminator("bdgrz.tenant-slug.surrender-requested", 1)]
public sealed record TenantSlugSurrenderRequested(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
