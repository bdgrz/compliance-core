using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

public static class RestrictedApplicationVisibilityFixture
{
    public static RestrictedApplicationVisibility Create(IAggregateReader reader,
        IPermissionAuthorizer permissions, IApplicationDirectoryReader? applications = null) =>
        new(permissions, new FixedScopedGrantAuthorizer([]), reader, applications);

    public static RestrictedApplicationVisibility Create(IAggregateReader reader,
        bool organizationPermission = false, params AccessGrantScope[] allowedScopes) =>
        new(new FixedPermissionAuthorizer(organizationPermission),
            new FixedScopedGrantAuthorizer(allowedScopes), reader);

    sealed class FixedPermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }

    sealed class FixedScopedGrantAuthorizer(AccessGrantScope[] allowedScopes)
        : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) =>
            ValueTask.FromResult(scopes.Any(allowedScopes.Contains));

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowedScopes.Any(scope =>
                scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                scope.Kind is AccessGrantScopeKind.Application or
                    AccessGrantScopeKind.SystemInstance));
    }
}
