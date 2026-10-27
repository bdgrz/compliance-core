using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The slug cannot be claimed for the tenant; the fact answers exactly one claim operation.
/// </summary>
[Discriminator("bdgrz.tenant-slug.registration-rejected", 1)]
public sealed record TenantSlugRegistrationRejected(Uuid TenantId, string Slug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
