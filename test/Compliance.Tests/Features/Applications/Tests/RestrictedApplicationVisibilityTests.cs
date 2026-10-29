using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class RestrictedApplicationVisibilityTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid SystemInstanceId = Uuid.CreateVersion4();
    static readonly Uuid OtherSystemInstanceId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldAllowRestrictedApplicationGivenOrganizationPermission()
    {
        // Arrange
        var permissions = new PermissionAuthorizer(true);
        var grants = new ScopedGrantAuthorizer();
        var visibility = Visibility(permissions, grants);

        // Act
        var applicationAllowed = await visibility.CanReadApplicationAsync(TenantId, UserId,
            ApplicationId);
        var instanceAllowed = await visibility.CanReadSystemInstanceAsync(TenantId, UserId,
            ApplicationId, SystemInstanceId);

        // Assert
        Assert.True(applicationAllowed);
        Assert.True(instanceAllowed);
        Assert.Empty(grants.RequestedScopes);
    }

    [Fact]
    public async Task ShouldAllowApplicationAndInstancesGivenApplicationScopedGrant()
    {
        // Arrange
        var permissions = new PermissionAuthorizer(false);
        var grants = new ScopedGrantAuthorizer(
            new AccessGrantScope(AccessGrantScopeKind.Application, ApplicationId));
        var visibility = Visibility(permissions, grants);

        // Act
        var applicationAllowed = await visibility.CanReadApplicationAsync(TenantId, UserId,
            ApplicationId);
        var instanceAllowed = await visibility.CanReadSystemInstanceAsync(TenantId, UserId,
            ApplicationId, SystemInstanceId);

        // Assert
        Assert.True(applicationAllowed);
        Assert.True(instanceAllowed);
    }

    [Fact]
    public async Task ShouldAllowOnlyGrantedInstanceGivenSystemInstanceScopedGrant()
    {
        // Arrange
        var permissions = new PermissionAuthorizer(false);
        var grants = new ScopedGrantAuthorizer(
            new AccessGrantScope(AccessGrantScopeKind.SystemInstance, SystemInstanceId));
        var visibility = Visibility(permissions, grants);

        // Act
        var applicationAllowed = await visibility.CanReadApplicationAsync(TenantId, UserId,
            ApplicationId);
        var instanceAllowed = await visibility.CanReadSystemInstanceAsync(TenantId, UserId,
            ApplicationId, SystemInstanceId);
        var siblingAllowed = await visibility.CanReadSystemInstanceAsync(TenantId, UserId,
            ApplicationId, OtherSystemInstanceId);

        // Assert
        Assert.False(applicationAllowed);
        Assert.True(instanceAllowed);
        Assert.False(siblingAllowed);
    }

    [Fact]
    public async Task ShouldSkipPermissionChecksGivenUnrestrictedApplication()
    {
        // Arrange
        var permissions = new PermissionAuthorizer(false);
        var grants = new ScopedGrantAuthorizer();
        var visibility = Visibility(permissions, grants, isRestricted: false);

        // Act
        var applicationAllowed = await visibility.CanReadApplicationAsync(TenantId, UserId,
            ApplicationId);
        var instanceAllowed = await visibility.CanReadSystemInstanceAsync(TenantId, UserId,
            ApplicationId, SystemInstanceId);

        // Assert
        Assert.True(applicationAllowed);
        Assert.True(instanceAllowed);
        Assert.Equal(0, permissions.CallCount);
        Assert.Empty(grants.RequestedScopes);
    }

    static RestrictedApplicationVisibility Visibility(PermissionAuthorizer permissions,
        ScopedGrantAuthorizer grants, bool isRestricted = true) =>
        new(permissions, grants, new ApplicationReader(isRestricted));

    sealed class ApplicationReader(bool isRestricted) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is DeclaredApplication application)
                Assert.True(application.Declare("Payroll", "Run payroll", null,
                    RbacIds.Member(TenantId, UserId), "Org Admin", DateTimeOffset.UtcNow,
                    isRestricted: isRestricted).IsSuccess);
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class PermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public int CallCount { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default)
        {
            CallCount++;
            Assert.Equal(TenantId, tenantId);
            Assert.Equal(UserId, userId);
            Assert.Equal(RbacIds.Member(TenantId, UserId), memberId);
            Assert.Equal(RbacPermissions.ApplicationRestrictedRead, permission);
            return ValueTask.FromResult(allowed);
        }
    }

    sealed class ScopedGrantAuthorizer(params AccessGrantScope[] allowedScopes)
        : IAccessGrantPermissionAuthorizer, IAccessGrantScopePermissionAuthorizer
    {
        public List<AccessGrantScope> RequestedScopes { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default)
        {
            RequestedScopes.AddRange(scopes);
            Assert.Equal(TenantId, tenantId);
            Assert.Equal(UserId, userId);
            Assert.Equal(RbacIds.Member(TenantId, UserId), memberId);
            Assert.Equal(RbacPermissions.ApplicationRestrictedRead, permission);
            return ValueTask.FromResult(scopes.Any(allowedScopes.Contains));
        }

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowedScopes.Any(scope =>
                scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                scope.Kind is AccessGrantScopeKind.Application or
                    AccessGrantScopeKind.SystemInstance));
    }
}
