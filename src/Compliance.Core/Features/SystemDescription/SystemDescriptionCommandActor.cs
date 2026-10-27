using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

static class SystemDescriptionCommandActor
{
    public static bool TryGet(ClaimsPrincipal principal, Uuid tenantId, out Uuid memberId,
        out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        if (tenantId == Uuid.Empty ||
            !UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId))
            return false;
        display = UserIdentityClaims.BdgrzDisplay(principal, userId);
        if (string.IsNullOrWhiteSpace(display))
            return false;
        memberId = RbacIds.Member(tenantId, userId);
        return true;
    }
}
