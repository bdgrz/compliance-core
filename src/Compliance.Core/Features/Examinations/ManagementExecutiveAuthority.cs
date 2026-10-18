using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
/// Resolves a management assertion signer from current, program-scoped RBAC grants and the
/// current direct client membership. A token role or the assertion's signer list alone is not
/// authority.
/// </summary>
public sealed class ManagementExecutiveAuthority(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions)
{
    static readonly (string Role, string Permission)[] ExecutiveRoles =
    [
        ("ceo", RbacPermissions.ManagementExecutiveCeo),
        ("ciso", RbacPermissions.ManagementExecutiveCiso),
    ];

    public async ValueTask<string?> ResolveSignerRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, IReadOnlyList<ManagementAssertionSigner> signers,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || signers is null)
            return null;

        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
            member.Affiliation != "client_personnel" || member.UserId != userId)
            return null;

        var authorizedRoles = new List<string>(ExecutiveRoles.Length);
        foreach (var (role, permission) in ExecutiveRoles)
        {
            if (!signers.Any(signer => signer.MemberId == memberId &&
                    StringComparer.OrdinalIgnoreCase.Equals(signer.Role, role)))
                continue;
            if (await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
                    permission, ct).ConfigureAwait(false))
                authorizedRoles.Add(role);
        }

        return authorizedRoles.Count == 1 ? authorizedRoles[0] : null;
    }
}
