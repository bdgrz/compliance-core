using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>A native activation decision bound to the exact authored administrator, email and command identity.</summary>
[Discriminator("bdgrz.tenant.activated", 2)]
public sealed record TenantActivatedV2(Uuid TenantId, Uuid FirstAdministratorUserId, string? FirstAdministratorEmail,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
