using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Assigns a member to a team. <paramref name="CommandId" /> is the logical identity of the assignment and an
///     exact retry replays the original acceptance. <paramref name="MemberEpisodeId" /> is the member's registration
///     episode the assignment binds to; <paramref name="ExpectedAssignmentRevision" /> and
///     <paramref name="ExpectedAssignmentGeneration" /> are the assignment owner's revision and assignment generation
///     the caller observed. Internal system callers send <see cref="Uuid.Empty" /> for the episode and negative
///     expectations to bind to the member's current episode and the owner's current state.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.assign", 1)]
public sealed record AssignTeamMember(Uuid CommandId, Uuid TenantId, Uuid TeamId, Uuid MemberId,
    Uuid MemberEpisodeId, long ExpectedAssignmentRevision, long ExpectedAssignmentGeneration)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
