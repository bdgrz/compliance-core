using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a member from a team. <paramref name="CommandId" /> is the logical identity of the removal and an
///     exact retry replays the original acceptance. <paramref name="MemberEpisodeId" /> is the member episode whose
///     assignment is removed; <paramref name="ExpectedAssignmentRevision" /> and
///     <paramref name="ExpectedAssignmentGeneration" /> are the assignment owner's revision and assignment generation
///     the caller observed. Internal system callers send <see cref="Uuid.Empty" /> for the episode and negative
///     expectations to remove the owner's current assignment.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.remove", 1)]
public sealed record RemoveTeamMember(Uuid CommandId, Uuid TenantId, Uuid TeamId, Uuid MemberId,
    Uuid MemberEpisodeId, long ExpectedAssignmentRevision, long ExpectedAssignmentGeneration)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
