using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Authorizers;
namespace Bdgrz.Compliance.Features.Providers.Services;

static class ProviderActor
{
    public static ActorReference From<T>(IRequestContext<T> context) where T : IProviderRequest
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject) ? subject :
            throw new InvalidOperationException("ProviderAuthorizer must reject this actor.");
        return ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
