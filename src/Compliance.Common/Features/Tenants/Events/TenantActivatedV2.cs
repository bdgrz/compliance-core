using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>A native activation decision bound to the exact authored administrator, email and command identity.</summary>
[Discriminator("bdgrz.tenant.activated", 2)]
public sealed record TenantActivatedV2(Uuid TenantId, Uuid FirstAdministratorUserId, string? FirstAdministratorEmail,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
