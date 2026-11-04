using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class TeamRoleScopes
{
    public static AccessGrantScope Normalize(Uuid tenant, AccessGrantScope? scope) =>
        scope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, tenant);

    public static bool IsValid(Uuid tenant, AccessGrantScope scope) => scope.Id != Uuid.Empty && (scope.Kind switch
    {
        AccessGrantScopeKind.Organization => scope.Id == tenant && scope.ResourceType is null,
        AccessGrantScopeKind.Program or AccessGrantScopeKind.Engagement => scope.ResourceType is null,
        AccessGrantScopeKind.SharedResource => !string.IsNullOrWhiteSpace(scope.ResourceType),
        _ => false,
    });

    public static string Key(AccessGrantScope scope) => string.Create(CultureInfo.InvariantCulture,
        $"{(int)scope.Kind}:{scope.Id}:{scope.ResourceType?.Length ?? 0}:{scope.ResourceType}");
}
