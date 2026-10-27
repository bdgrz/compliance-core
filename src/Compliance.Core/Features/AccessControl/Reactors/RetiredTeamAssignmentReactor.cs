using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

public sealed partial class RetiredTeamAssignmentReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-team-members"), "RetiredTeamAssignmentV1"),
      IReactorHandler<TeamMemberAssigned>
{
    public async ValueTask HandleAsync(IReactorContext<TeamMemberAssigned> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (context.Source.Stream != new TeamMember(source.TenantId, source.TeamId, source.MemberId).Stream)
            throw new InvalidOperationException("Team cleanup requires its exact assignment source.");
        var member = await reader.HydrateAsync(Member.ForVerification(source.TenantId, source.MemberId,
            source.MembershipEpisodeId), ct).ConfigureAwait(false);
        if (member.TerminationProof is not { } proof)
        {
            if (member.IsRegistered && member.MembershipEpisodeId == source.MembershipEpisodeId)
                return;
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "Membership retirement authority is not available yet.", isTransient: true));
        }
        var request = MemberRetirementEffects.Team(source, context.Source.ResourceOffset, proof.EventId);
        await bus.SendReactionAsync(request, context, ct).ConfigureAwait(false);
    }
}
