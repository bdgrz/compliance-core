using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class TenantAccessAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldAllowSystemActorGivenMissingMemberPermission()
    {
        // Arrange
        var authorizer = new TenantAccessAuthorizer(new RecordingPermissionAuthorizer(false), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, RequestActor.System);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var authorizer = new TenantAccessAuthorizer(new RecordingPermissionAuthorizer(true), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(
            request,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc")));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowActorGivenTenantAccessPermission()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new TenantAccessAuthorizer(permissions, new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacIds.Member(TenantId, UserId), Assert.Single(permissions.MemberIds));
        Assert.Equal(RbacPermissions.TenantAccess, Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingTenantAccessPermission()
    {
        // Arrange
        var authorizer = new TenantAccessAuthorizer(new RecordingPermissionAuthorizer(false), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenStandingTeamPermission()
    {
        // Arrange
        var authorizer = new TenantAccessAuthorizer(new RecordingPermissionAuthorizer(true),
            new ActiveTenant(), new FixedMembershipDirectory(true, "firm_staff"));
        var context = new RequestContext<ITenantAccessRequest>(
            new GetTeam(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenySuspendedActorGivenHistoricalTenantAccessGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new TenantAccessAuthorizer(permissions, new ActiveTenant(),
            new FixedMembershipDirectory(true, isSuspended: true));
        var context = new RequestContext<ITenantAccessRequest>(
            new GetTeam(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideTenantGivenDeprovisionedActorAndHistoricalTenantAccessGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new TenantAccessAuthorizer(permissions, new ActiveTenant(),
            new FixedMembershipDirectory(true, isDeprovisioned: true));
        var context = new RequestContext<ITenantAccessRequest>(
            new GetTeam(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideTenantGivenExpiredGuestMembership()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var expiredOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var authorizer = new TenantAccessAuthorizer(permissions, new ActiveTenant(),
            new FixedMembershipDirectory(true, "guest", membershipExpiresOn: expiredOn));
        var context = new RequestContext<ITenantAccessRequest>(
            new GetTeam(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(permissions.Permissions);
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldHideTenantGivenNonmemberActor()
    {
        // Arrange
        var authorizer = new TenantAccessAuthorizer(new RecordingPermissionAuthorizer(true), new ActiveTenant(), new FixedMembershipDirectory(false));
        var context = new RequestContext<ITenantAccessRequest>(
            new GetTeam(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }
}
