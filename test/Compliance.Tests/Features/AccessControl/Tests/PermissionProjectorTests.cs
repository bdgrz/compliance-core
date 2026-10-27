using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessControl.Projectors;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PermissionProjectorTests
{
    [Fact]
    public void ShouldInvalidateOnlyRemovedTeamPathGivenPermissionIsGrantedByMultipleTeams()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var removedTeamId = Uuid.CreateVersion4();
        var otherTeamId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var permission = RbacPermissions.TenantAccess;
        DomainEvent removal = TeamMemberFacts.Removed(tenantId, removedTeamId, memberId, Uuid.Empty, Uuid.CreateVersion4());
        var removedPath = new MemberAccessEdge(removedTeamId, roleId, [permission]);
        var retainedPath = new MemberAccessEdge(otherTeamId, roleId, [permission]);

        // Act
        var removesOriginalGrant = PermissionProjector.RevokesAccessPath(removal, memberId,
            permission, removedPath);
        var removesAlternativeGrant = PermissionProjector.RevokesAccessPath(removal, memberId,
            permission, retainedPath);

        // Assert
        Assert.True(removesOriginalGrant);
        Assert.False(removesAlternativeGrant);
    }
}
