using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

static class TenantIdentityProviderCommandActor
{
    public static bool TryGet(ClaimsPrincipal principal, RequestInvocation invocation,
        Uuid tenantId, out Uuid memberId, out string display)
    {
        memberId = Uuid.Empty;
        display = string.Empty;
        if (invocation is not HttpInvocation || RequestActor.IsSystem(principal) ||
            tenantId == Uuid.Empty || !UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId))
            return false;

        memberId = RbacIds.Member(tenantId, userId);
        display = UserIdentityClaims.BdgrzDisplay(principal, userId);
        return memberId != Uuid.Empty && !string.IsNullOrWhiteSpace(display);
    }
}
