using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Resolves a rollover approver from current client membership and scoped RBAC grants.</summary>
public sealed class ProgramRolloverApproverAuthority(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions)
{
    public async ValueTask<string?> ResolveRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId))
            return null;

        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
            member.Affiliation != "client_personnel" || member.UserId != userId ||
            !await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
                RbacPermissions.ProgramManage, ct).ConfigureAwait(false))
            return null;

        var ceo = await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
            RbacPermissions.ManagementExecutiveCeo, ct).ConfigureAwait(false);
        var ciso = await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
            RbacPermissions.ManagementExecutiveCiso, ct).ConfigureAwait(false);
        if (ceo && ciso)
            return null;
        if (ceo)
            return "ceo";
        return ciso ? "ciso" : "compliance_lead";
    }
}
