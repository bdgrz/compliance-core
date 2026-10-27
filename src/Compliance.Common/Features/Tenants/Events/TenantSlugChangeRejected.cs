using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     A requested slug change ended because the slug owner refused the new slug.
/// </summary>
[Discriminator("bdgrz.tenant-slug.change-rejected", 1)]
public sealed record TenantSlugChangeRejected(Uuid TenantId, string OldSlug, string NewSlug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
