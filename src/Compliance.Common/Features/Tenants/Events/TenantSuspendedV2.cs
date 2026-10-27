using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>An attributed native lifecycle decision with its immutable original outcome.</summary>
[Discriminator("bdgrz.tenant.suspended", 2)]
public sealed record TenantSuspendedV2(Uuid TenantId, Uuid OperatorUserId, string Reason, DateTimeOffset OccurredAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef OperationResult)
    : DomainEvent, INativeOperationFact;
