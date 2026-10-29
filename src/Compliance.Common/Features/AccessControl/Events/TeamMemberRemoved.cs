using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A member's team assignment ended. The fact is the native acceptance of the logical
///     <paramref name="CommandId" /> on the team-member stream; a retirement removal carries the termination event
///     that proves the closed membership episode.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.removed", 2)]
public sealed record TeamMemberRemoved(Uuid TenantId, Uuid TeamId, Uuid MemberId, Uuid MembershipEpisodeId,
    Uuid AssignmentEventId, Uuid? TerminationEventId, Uuid CommandId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
