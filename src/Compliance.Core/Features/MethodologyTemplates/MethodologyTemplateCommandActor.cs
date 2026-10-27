using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

static class MethodologyTemplateCommandActor
{
    public static (Uuid MemberId, string Display) For<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId) where TRequest : IRequestBase
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) ||
            RequestActor.IsSystem(context.Actor))
            throw new InvalidOperationException("The request authorizer must require a personal Bdgrz user.");

        return (RbacIds.Member(tenantId, userId), UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
