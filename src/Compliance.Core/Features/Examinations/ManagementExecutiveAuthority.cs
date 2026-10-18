using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
/// Resolves CEO/CISO authority from current, program-scoped RBAC grants and the current direct
/// client membership. Token role claims and client-authored signer lists alone are not authority.
/// </summary>
public sealed class ManagementExecutiveAuthority(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions)
{
    static readonly (string Role, string Permission)[] ExecutiveRoles =
    [
        ("ceo", RbacPermissions.ManagementExecutiveCeo),
        ("ciso", RbacPermissions.ManagementExecutiveCiso),
    ];

    public ValueTask<string?> ResolveExecutiveRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, CancellationToken ct) =>
        ResolveRoleAsync(tenantId, programId, userId, memberId, ExecutiveRoles, ct);

    public async ValueTask<string?> ResolveSignerRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, IReadOnlyList<ManagementAssertionSigner> signers,
        CancellationToken ct)
    {
        if (signers is null)
            return null;

        var eligibleRoles = ExecutiveRoles.Where(item => signers.Any(signer =>
            signer.MemberId == memberId &&
            StringComparer.OrdinalIgnoreCase.Equals(signer.Role, item.Role))).ToArray();
        return await ResolveRoleAsync(tenantId, programId, userId, memberId, eligibleRoles, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<string?> ResolveRoleAsync(Uuid tenantId, Uuid programId, Uuid userId,
        Uuid memberId, (string Role, string Permission)[] eligibleRoles,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || eligibleRoles.Length == 0)
            return null;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
            member.Affiliation != "client_personnel" || member.UserId != userId)
            return null;

        var authorizedRoles = new List<string>(eligibleRoles.Length);
        foreach (var (role, permission) in eligibleRoles)
        {
            if (await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
                    permission, ct).ConfigureAwait(false))
                authorizedRoles.Add(role);
        }

        return authorizedRoles.Count == 1 ? authorizedRoles[0] : null;
    }
}
