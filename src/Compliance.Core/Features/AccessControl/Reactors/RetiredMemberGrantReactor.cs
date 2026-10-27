using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

public sealed partial class RetiredMemberGrantReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("access-grants"), "RetiredMemberGrantV1"),
      IReactorHandler<AccessGrantIssued>
{
    public async ValueTask HandleAsync(IReactorContext<AccessGrantIssued> context, CancellationToken ct)
    {
        if (context.Trigger.Terms.Principal.Kind != AccessGrantPrincipalKind.Member)
            return;
        var source = context.Trigger;
        if (context.Source.Stream != new AccessGrant(source.TenantId, source.GrantId).Stream)
            throw new InvalidOperationException("Grant cleanup requires its exact issued source.");
        var episodeId = source.MembershipEpisodeId ?? Uuid.Empty;
        if (episodeId == Uuid.Empty)
            throw new InvalidOperationException("A member grant requires its exact membership episode.");
        var member = await reader.HydrateAsync(Member.ForVerification(source.TenantId, source.Terms.Principal.Id,
            episodeId), ct).ConfigureAwait(false);
        if (member.TerminationProof is not { } proof)
        {
            if (member.IsRegistered && member.MembershipEpisodeId == episodeId)
                return;
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "Membership retirement authority is not available yet.", isTransient: true));
        }
        var request = MemberRetirementEffects.Grant(source, context.Source.ResourceOffset, proof.EventId);
        await bus.SendReactionAsync(request, context, ct).ConfigureAwait(false);
    }
}
