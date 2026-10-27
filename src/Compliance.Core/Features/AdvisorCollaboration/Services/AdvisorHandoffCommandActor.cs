using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Services;

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
