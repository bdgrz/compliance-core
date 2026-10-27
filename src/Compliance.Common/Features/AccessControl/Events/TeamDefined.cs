using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A team was defined. The fact is the native acceptance of the logical operation that created it.
/// </summary>
[Discriminator("bdgrz.rbac.team.defined", 1)]
public sealed record TeamDefined(Uuid TenantId, Uuid TeamId, string Name, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
