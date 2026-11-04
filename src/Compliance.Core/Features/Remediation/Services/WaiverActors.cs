using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Remediation.Services;

static class WaiverActors
{
    public static Result<ActorReference> Member(ClaimsPrincipal principal, Uuid tenantId)
    {
        if (RequestActor.IsSystem(principal) || !UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId))
            return Result<ActorReference>.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Product-waiver changes require a canonical signed-in member."));
        return ActorReference.ForMember(RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(principal, userId));
    }

    public static RequestError? PersonalReviewError(IRequestContext context) =>
        context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor)
            ? new RequestError(RequestErrorKind.Forbidden, "Waiver review and revocation require a personal HTTP request.")
            : null;
}
