using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

public sealed class RestrictedComplianceRecordVisibilityTests
{
    [Fact]
    public async Task ShouldAllowNonRestrictedContentWithoutRestrictedGrantGivenTenantIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new StubPermissions(false);
        var visibility = new RestrictedComplianceRecordVisibility(permissions);

        // Act
        var allowed = await visibility.CanReadAsync(tenantId, RequestActor.System,
            "confidential", []);

        // Assert
        Assert.True(allowed);
        Assert.Equal(0, permissions.CallCount);
    }

    [Fact]
    public async Task ShouldAllowRestrictedContentToRecordedAssessorGivenMatchingMemberIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var permissions = new StubPermissions(false);
        var visibility = new RestrictedComplianceRecordVisibility(permissions);

        // Act
        var allowed = await visibility.CanReadAsync(tenantId, Actor(userId),
            "restricted", [memberId]);

        // Assert
        Assert.True(allowed);
        Assert.Equal(0, permissions.CallCount);
    }

    [Fact]
    public async Task ShouldRequireDedicatedPermissionForOtherRestrictedReadersGivenProgramAccess()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new StubPermissions(true);
        var visibility = new RestrictedComplianceRecordVisibility(permissions);

        // Act
        var allowed = await visibility.CanReadAsync(tenantId, Actor(userId),
            "restricted", []);

        // Assert
        Assert.True(allowed);
        Assert.Equal(1, permissions.CallCount);
        Assert.Equal(RbacPermissions.ComplianceRecordRestrictedRead, permissions.Permission);
        Assert.Equal(RbacIds.Member(tenantId, userId), permissions.MemberId);
    }

    [Fact]
    public async Task ShouldFailClosedGivenAnonymousActorOrUnclassifiedRestrictedContent()
    {
        // Arrange
        var visibility = new RestrictedComplianceRecordVisibility(new StubPermissions(true));

        // Act
        var anonymous = await visibility.CanReadAsync(Uuid.CreateVersion4(),
            RequestActor.System, "restricted", []);
        var invalid = await visibility.CanReadAsync(Uuid.CreateVersion4(),
            RequestActor.System, "unknown", []);

        // Assert
        Assert.False(anonymous);
        Assert.False(invalid);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class StubPermissions(bool allowed) : IPermissionAuthorizer
    {
        public int CallCount { get; private set; }
        public string? Permission { get; private set; }
        public Uuid MemberId { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default)
        {
            CallCount++;
            Permission = permission;
            MemberId = memberId;
            return ValueTask.FromResult(allowed);
        }
    }
}
