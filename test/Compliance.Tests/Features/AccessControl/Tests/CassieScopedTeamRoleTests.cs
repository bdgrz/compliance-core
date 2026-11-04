using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieScopedTeamRoleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPageAndRemoveExactScopesGivenSameTeamAndRoleAtThreeScopes(bool reverse)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var byTeam = new CassieTeamRoleDirectoryReader(database.Database);
        await using var byRole = new CassieRoleTeamDirectoryReader(database.Database);
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var scopes = Enumerable.Range(0, 3).Select(_ => new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4())).ToArray();
        var actor = new ActorAttribution("setup", "Portia");
        var assignments = scopes.Select(scope => new TeamRoleAssigned(tenant, team, role, Uuid.CreateVersion4(), 0, actor, scope)).ToArray();
        CassieProjectionRepository selected = reverse ? byRole : byTeam;
        var identity = new CheckpointIdentity(reverse ? "RoleTeamDirectory" : "TeamRoleDirectory",
            EventStreamPattern.ForPattern(tenant.ToString()));
        await using (var batch = await selected.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            foreach (var fact in assignments)
                if (reverse)
                    await byRole.ApplyAsync(fact);
                else
                    await byTeam.ApplyAsync(fact);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var found = new List<AccessGrantScope>();
        string? cursor = null;
        do
        {
            if (reverse)
            {
                var page = await byRole.ListAsync(tenant, role, 1, cursor, null, true);
                found.Add(Assert.Single(page.Items).Scope!);
                cursor = page.NextCursor;
            }
            else
            {
                var page = await byTeam.ListAsync(tenant, team, 1, cursor, null, false);
                found.Add(Assert.Single(page.Items).Scope!);
                cursor = page.NextCursor;
            }
        } while (cursor is not null);
        var removed = new TeamRoleRemoved(tenant, team, role, Uuid.CreateVersion4(), 1, actor, assignments[1].CommandId, scopes[1]);
        await using (var batch = await selected.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            if (reverse)
                await byRole.ApplyAsync(removed);
            else
                await byTeam.ApplyAsync(removed);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var retained = reverse
            ? (await byRole.ListAsync(tenant, role, 50, null, null, false)).Items.Select(item => item.Scope).ToArray()
            : (await byTeam.ListAsync(tenant, team, 50, null, null, false)).Items.Select(item => item.Scope).ToArray();

        // Assert
        Assert.Equal(scopes.Length, found.Count);
        Assert.True(scopes.ToHashSet().SetEquals(found));
        Assert.Equal(2, retained.Length);
        Assert.DoesNotContain(scopes[1], retained);
        Assert.Contains(scopes[0], retained);
        Assert.Contains(scopes[2], retained);
    }
}
