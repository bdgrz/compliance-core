using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>An attributed native lifecycle decision with its immutable original outcome.</summary>
[Discriminator("bdgrz.tenant.reactivated", 2)]
public sealed record TenantReactivatedV2(Uuid TenantId, Uuid OperatorUserId, string Reason, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact;
