using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     A requested slug change ended because the slug owner refused the new slug.
/// </summary>
[Discriminator("bdgrz.tenant-slug.change-rejected", 1)]
public sealed record TenantSlugChangeRejected(Uuid TenantId, string OldSlug, string NewSlug, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
