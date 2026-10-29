using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

public interface IAccessGrantRolePermissionAuthorizer
{
    ValueTask<bool> IsAllowedAsRoleAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        Uuid programId, Uuid roleId, string permission, CancellationToken ct = default);
}
