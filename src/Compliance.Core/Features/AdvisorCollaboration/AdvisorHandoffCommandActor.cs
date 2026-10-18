using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

static class AdvisorHandoffCommandActor
{
    public static bool TryGet<TRequest>(IRequestContext<TRequest> context, Uuid tenantId,
        out ActorReference actor) where TRequest : IRequestBase
    {
        actor = null!;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            tenantId == Uuid.Empty || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return false;

        actor = ActorReference.ForMember(RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return true;
    }
}
