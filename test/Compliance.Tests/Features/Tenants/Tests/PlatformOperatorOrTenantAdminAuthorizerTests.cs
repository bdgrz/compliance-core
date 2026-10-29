using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class PlatformOperatorOrTenantAdminAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowOrgAdminGivenActiveTenantRbacManage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(tenantId, userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.TenantRbacManage,
            Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldDenyTenantMemberGivenMissingTenantRbacManage()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.TenantRbacManage,
            Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task ShouldForbidCallerWithoutTargetTenantMembershipGivenSlugChange()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(member: false), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenTenantRbacManagePermission()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true, "firm_staff"), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideSuspendedTenantGivenHistoricalAdminGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true, isSuspended: true),
            permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldAllowPlatformOperatorGivenNoTenantMembership()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var authorizer = new PlatformOperatorOrTenantAdminAuthorizer(
            new FixedOperatorAccess(userId), new FixedMembershipDirectory(member: false),
            new ActiveTenant(), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(), userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);
        var request = new ChangeTenantSlug(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "acme-next", 0);
        var context = new RequestContext<IPlatformOperatorOrTenantAdminRequest>(request,
            new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Unauthorized, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    static PlatformOperatorOrTenantAdminAuthorizer Authorizer(
        ITenantMembershipDirectoryReader memberships, IPermissionAuthorizer permissions) =>
        new(new FixedOperatorAccess(), memberships, new ActiveTenant(), permissions);

    static RequestContext<IPlatformOperatorOrTenantAdminRequest> Context(Uuid tenantId,
        Uuid userId) => new(new ChangeTenantSlug(Uuid.CreateVersion4(), tenantId, "acme-next", 0), Actor(userId));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
