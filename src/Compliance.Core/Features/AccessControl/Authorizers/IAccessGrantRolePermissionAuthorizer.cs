using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

public interface IAccessGrantRolePermissionAuthorizer
{
    /// <summary>Checks the current organization-scoped team role, without inferring it from a permission or program grant.</summary>
    ValueTask<bool> IsAllowedAsOrganizationRoleAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        Uuid roleId, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);

    ValueTask<bool> IsAllowedAsRoleAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        Uuid programId, Uuid roleId, string permission, CancellationToken ct = default);
}
