using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieRolePermissionDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryPermissionGivenRoleQuery()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await SeedAsync(database.Database, RoleId, "controls.read");
        await SeedAsync(database.Database, RoleId, "controls.manage");
        await using var reader = new CassieRolePermissionDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldReturnPermissionsOnlyGivenRequestedRole()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var otherRoleId = Uuid.CreateVersion4();
        await SeedAsync(database.Database, RoleId, "controls.read");
        await SeedAsync(database.Database, otherRoleId, "controls.manage");
        await using var reader = new CassieRolePermissionDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal("controls.read", result.Permission);
    }

    [Fact]
    public async Task ShouldFilterPermissionsGivenSubstring()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await SeedAsync(database.Database, RoleId, "controls.read");
        await SeedAsync(database.Database, RoleId, "tenant.access");
        await using var reader = new CassieRolePermissionDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, "trols", descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal("controls.read", result.Permission);
    }

    [Fact]
    public async Task ShouldExposeExactRemovalInputsGivenAReplacementAssignment()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var reader = new CassieRolePermissionDirectoryReader(database.Database);
        var assignment = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(RbacPermissionFacts.Assigned(TenantId, RoleId, "controls.read"));
            await reader.ApplyAsync(RbacPermissionFacts.Removed(TenantId, RoleId, "controls.read"));
            await reader.ApplyAsync(new RolePermissionAssigned(TenantId, RoleId, "controls.read", assignment, 2,
                RbacPermissionFacts.Actor));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var page = await reader.ListAsync(TenantId, RoleId, null, null, null, descending: false);

        // Assert
        var row = Assert.Single(page.Items);
        Assert.Equal(assignment, row.AssignmentId);
        Assert.Equal(3UL, row.Revision);
    }

    static async Task SeedAsync(CassieProjectionDatabase database, Uuid roleId, string permission)
    {
        await using var repository = new CassieRolePermissionDirectoryReader(database);
        var identity = new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(RbacPermissionFacts.Assigned(TenantId, roleId, permission));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
