using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A member was assigned to a team for one membership episode. The fact is the native acceptance of the
///     logical <paramref name="CommandId" /> on the team-member stream.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.assigned", 1)]
public sealed record TeamMemberAssigned(Uuid TenantId, Uuid TeamId, Uuid MemberId, Uuid MembershipEpisodeId,
    Uuid CommandId, OperationProvenance Operation, ImmutableContentRef? FirstResponse)
    : DomainEvent, INativeOperationFact;
