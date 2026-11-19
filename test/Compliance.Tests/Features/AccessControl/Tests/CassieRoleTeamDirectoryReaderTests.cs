using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieRoleTeamDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryTeamGivenRoleQuery()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(database.Database, RoleId, first);
        await SeedAsync(database.Database, RoleId, second);
        await using var reader = new CassieRoleTeamDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldReturnTeamsOnlyGivenRequestedRole()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var otherRoleId = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var otherTeam = Uuid.CreateVersion4();
        await SeedAsync(database.Database, RoleId, team);
        await SeedAsync(database.Database, otherRoleId, otherTeam);
        await using var reader = new CassieRoleTeamDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(
            TenantId, RoleId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(team, result.TeamId);
    }

    [Fact]
    public async Task ShouldExposeTheSameEpisodeAndRevisionGivenEitherDirectoryDirection()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var team = Uuid.CreateVersion4();
        var assignment = Uuid.CreateVersion4();
        var fact = new TeamRoleAssigned(TenantId, team, RoleId, assignment, 4, RbacTeamRoleFacts.Actor);
        await using var byTeam = new CassieTeamRoleDirectoryReader(database.Database);
        await using var byRole = new CassieRoleTeamDirectoryReader(database.Database);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        await using (var batch = await byTeam.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("TeamRoleDirectory", pattern), ProjectionCheckpoint.Start)))
        {
            await byTeam.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var batch = await byRole.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("RoleTeamDirectory", pattern), ProjectionCheckpoint.Start)))
        {
            await byRole.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var teamPage = await byTeam.ListAsync(TenantId, team, null, null, null, descending: false);
        var rolePage = await byRole.ListAsync(TenantId, RoleId, null, null, null, descending: false);

        // Assert
        var teamRow = Assert.Single(teamPage.Items);
        var roleRow = Assert.Single(rolePage.Items);
        Assert.Equal(assignment, teamRow.AssignmentId);
        Assert.Equal(assignment, roleRow.AssignmentId);
        Assert.Equal(5UL, teamRow.Revision);
        Assert.Equal(teamRow.Revision, roleRow.Revision);
        Assert.Equal(team, roleRow.TeamId);
        Assert.Equal(RoleId, teamRow.RoleId);
    }

    static async Task SeedAsync(CassieProjectionDatabase database, Uuid roleId, Uuid teamId)
    {
        await using var repository = new CassieRoleTeamDirectoryReader(database);
        var identity = new CheckpointIdentity("RoleTeamDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(RbacTeamRoleFacts.Assigned(TenantId, teamId, roleId));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
