using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The tenant does not own the slug; the fact answers exactly one surrender operation.
/// </summary>
[Discriminator("bdgrz.tenant-slug.surrender-rejected", 1)]
public sealed record TenantSlugSurrenderRejected(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
