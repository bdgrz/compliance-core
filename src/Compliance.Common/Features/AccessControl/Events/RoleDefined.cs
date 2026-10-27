using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A role was defined. The fact is the native acceptance of the logical operation that created it.
/// </summary>
[Discriminator("bdgrz.rbac.role.defined", 1)]
public sealed record RoleDefined(Uuid TenantId, Uuid RoleId, string Name, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
