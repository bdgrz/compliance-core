using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A team was deleted. The fact is the native acceptance of the deletion and the source of team cleanup.
/// </summary>
[Discriminator("bdgrz.rbac.team.deleted", 1)]
public sealed record TeamDeleted(Uuid TenantId, Uuid TeamId, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
