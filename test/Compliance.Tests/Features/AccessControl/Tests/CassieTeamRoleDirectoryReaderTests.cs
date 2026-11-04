using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieTeamRoleDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryRoleGivenTeamQuery()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, TeamId, Uuid.CreateVersion4()));
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, TeamId, Uuid.CreateVersion4()));
        await using var reader = new CassieTeamRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(TeamId, item.TeamId));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldReturnRolesOnlyGivenRequestedTeam()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var role = Uuid.CreateVersion4();
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, TeamId, role));
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()));
        await using var reader = new CassieTeamRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(role, result.RoleId);
    }

    [Fact]
    public async Task ShouldDropRoleGivenTeamRoleRemoved()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var kept = Uuid.CreateVersion4();
        var removed = Uuid.CreateVersion4();
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, TeamId, kept));
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Assigned(TenantId, TeamId, removed));
        await ApplyAsync(database.Database, RbacTeamRoleFacts.Removed(TenantId, TeamId, removed));
        await using var reader = new CassieTeamRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(kept, Assert.Single(page.Items).RoleId);
    }

    static async Task ApplyAsync(CassieProjectionDatabase database, DomainEvent domainEvent)
    {
        await using var repository = new CassieTeamRoleDirectoryReader(database);
        var identity = new CheckpointIdentity("TeamRoleDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
