using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A role was renamed. The fact is the native acceptance of the rename.
/// </summary>
[Discriminator("bdgrz.rbac.role.renamed", 1)]
public sealed record RoleRenamed(Uuid TenantId, Uuid RoleId, string Name, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
