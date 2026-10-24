using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A role was renamed. The fact is the native acceptance of the rename.
/// </summary>
[Discriminator("bdgrz.rbac.role.renamed", 1)]
public sealed record RoleRenamed(Uuid TenantId, Uuid RoleId, string Name, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
