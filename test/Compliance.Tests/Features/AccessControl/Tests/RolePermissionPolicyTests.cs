using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class RolePermissionPolicyTests
{
    [Fact]
    public void ShouldRefuseUnknownPermissionWithoutFactGivenCustomRole()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var owner = new RolePermission(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "invented.permission", id);

        // Act
        var result = owner.Assign(id, 0, new ActorAttribution("administrator", "bdgrz"));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<RolePermission>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseHumanPermissionChangesWithoutFactGivenBuiltInRole(bool remove)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var owner = new RolePermission(tenant, BuiltInRbac.ViewerRoleId(tenant), RbacPermissions.TenantAccess, command);
        var actor = new ActorAttribution("administrator", "bdgrz");

        // Act
        var result = remove ? owner.Remove(command, 0, Uuid.CreateVersion4(), actor) : owner.Assign(command, 0, actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<RolePermission>(owner).PendingEvents);
    }
}
