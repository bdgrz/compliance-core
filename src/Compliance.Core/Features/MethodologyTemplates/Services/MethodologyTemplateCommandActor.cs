using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

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
