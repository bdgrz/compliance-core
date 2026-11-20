using System.Globalization;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Assigns a member to a team. The addressed relationship and observed assignment revision provide its logical
///     identity, so an exact retry replays the original acceptance. <paramref name="MemberEpisodeId" /> is the member's registration
///     episode the assignment binds to; <paramref name="ExpectedAssignmentRevision" /> and
///     <paramref name="ExpectedAssignmentGeneration" /> are the assignment owner's revision and assignment generation
///     the caller observed. Internal system callers send <see cref="Uuid.Empty" /> for the episode and negative
///     expectations to bind to the member's current episode and the owner's current state.
/// </summary>
[Discriminator("bdgrz.rbac.team-member.assign", 2)]
public sealed record AssignTeamMember(Uuid TenantId, Uuid TeamId, Uuid MemberId,
    Uuid MemberEpisodeId, long ExpectedAssignmentRevision, long ExpectedAssignmentGeneration)
    : IRequest<AdministratorTransitionResponse>, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId => Uuid.CreateVersion5(TeamId, string.Create(CultureInfo.InvariantCulture,
        $"team-member:assign\n{TenantId}\n{MemberId}\n{MemberEpisodeId}\n{ExpectedAssignmentRevision}\n{ExpectedAssignmentGeneration}"));

    Uuid IIdentifiedRequest.RequestId => OperationId;
}
