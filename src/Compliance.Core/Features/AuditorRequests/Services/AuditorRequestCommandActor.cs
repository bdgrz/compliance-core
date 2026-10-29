using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Services;

static class AuditorRequestCommandActor
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
        return true;
    }
}
