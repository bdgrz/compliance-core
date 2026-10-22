using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class RbacOperationActor
{
    public static ActorAttribution From(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(item => item.IsAuthenticated);
        var subject = identity?.FindFirst(ClaimTypes.NameIdentifier) ?? identity?.FindFirst("sub");
        return subject is null || string.IsNullOrWhiteSpace(subject.Value) || string.IsNullOrWhiteSpace(subject.Issuer)
            ? throw new InvalidOperationException("RBAC authorization must establish a stable actor.")
            : new ActorAttribution(subject.Value, subject.Issuer);
    }
}
