using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
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

public sealed class FitzRolePermissionDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryPermissionGivenRoleQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, RoleId, "controls.manage");
        var reader = new FitzRolePermissionDirectoryReader(client);

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
        var client = new InMemoryKvClient();
        var otherRoleId = Uuid.CreateVersion4();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, otherRoleId, "controls.manage");
        var reader = new FitzRolePermissionDirectoryReader(client);

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
        var client = new InMemoryKvClient();
        await SeedAsync(client, RoleId, "controls.read");
        await SeedAsync(client, RoleId, "tenant.access");
        var reader = new FitzRolePermissionDirectoryReader(client);

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
        var reader = new FitzRolePermissionDirectoryReader(new InMemoryKvClient());
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

    static async Task SeedAsync(InMemoryKvClient client, Uuid roleId, string permission)
    {
        var repository = new FitzRolePermissionDirectoryReader(client);
        var identity = new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(RbacPermissionFacts.Assigned(TenantId, roleId, permission));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
