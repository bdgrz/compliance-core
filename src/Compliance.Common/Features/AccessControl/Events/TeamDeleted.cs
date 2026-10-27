using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A team was deleted. The fact is the native acceptance of the deletion and the source of team cleanup.
/// </summary>
[Discriminator("bdgrz.rbac.team.deleted", 1)]
public sealed record TeamDeleted(Uuid TenantId, Uuid TeamId, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
