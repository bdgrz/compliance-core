using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A role was deleted. The fact is the native acceptance of the deletion and the source of role cleanup.
/// </summary>
[Discriminator("bdgrz.rbac.role.deleted", 1)]
public sealed record RoleDeleted(Uuid TenantId, Uuid RoleId, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
