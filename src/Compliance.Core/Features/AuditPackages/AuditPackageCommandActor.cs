using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

static class AuditPackageCommandActor
{
    public static bool TryGet(ClaimsPrincipal principal, RequestInvocation invocation, Uuid tenantId,
        out Uuid memberId, out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        if (invocation is not HttpInvocation || RequestActor.IsSystem(principal) ||
            tenantId == Uuid.Empty || !UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId))
            return false;
        display = UserIdentityClaims.BdgrzDisplay(principal, userId);
        if (string.IsNullOrWhiteSpace(display))
            return false;
        memberId = RbacIds.Member(tenantId, userId);
        return memberId != Uuid.Empty;
    }
}
