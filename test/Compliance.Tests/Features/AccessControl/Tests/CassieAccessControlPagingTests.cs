using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieAccessControlPagingTests
{
    [Theory]
    [InlineData("team", false, false)]
    [InlineData("team", false, true)]
    [InlineData("team", true, false)]
    [InlineData("team", true, true)]
    [InlineData("role", false, false)]
    [InlineData("role", false, true)]
    [InlineData("role", true, false)]
    [InlineData("role", true, true)]
    [InlineData("member", false, false)]
    [InlineData("member", false, true)]
    [InlineData("member", true, false)]
    [InlineData("member", true, true)]
    [InlineData("permission", false, false)]
    [InlineData("permission", false, true)]
    [InlineData("permission", true, false)]
    [InlineData("permission", true, true)]
    [InlineData("role_team", false, false)]
    [InlineData("role_team", false, true)]
    [InlineData("role_team", true, false)]
    [InlineData("role_team", true, true)]
    [InlineData("team_role", false, false)]
    [InlineData("team_role", false, true)]
    [InlineData("team_role", true, false)]
    [InlineData("team_role", true, true)]
    public async Task ShouldReturnEachMatchingRowOnceGivenNativeKeysetPages(string kind, bool descending, bool search)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, tenant, kind, descending, search ? "a" : null);
        await using (var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(scenario.Component, EventStreamPattern.ForPattern(tenant.ToString())), ProjectionCheckpoint.Start)))
        {
            await scenario.Seed();
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var found = new List<string>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        // Act
        do
        {
            var page = await scenario.List(cursor);
            Assert.Single(page.Items);
            found.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is not null)
                Assert.True(cursors.Add(cursor));
        } while (cursor is not null && found.Count < 10);

        // Assert
        Assert.Null(cursor);
        Assert.Equal(scenario.Expected, found);
        Assert.Equal(found.Count, found.Distinct(StringComparer.Ordinal).Count());
    }

    static PagingScenario Create(CassieProjectionDatabase database, Uuid tenant, string kind, bool descending, string? search)
    {
        var ids = new[]
        {
            Uuid.Parse("a0000000-0000-4000-8000-000000000001", System.Globalization.CultureInfo.InvariantCulture),
            Uuid.Parse("a0000000-0000-4000-8000-000000000002", System.Globalization.CultureInfo.InvariantCulture),
            Uuid.Parse("a0000000-0000-4000-8000-000000000003", System.Globalization.CultureInfo.InvariantCulture),
            Uuid.Parse("f0000000-0000-4000-8000-000000000004", System.Globalization.CultureInfo.InvariantCulture),
        };
        var names = new[] { "Match", "MATCH", "Zeta match", "Skip" };
        var permissions = new[] { "match.a", "match.b", "match.c", "skip" };
        var parent = Uuid.CreateVersion4();
        switch (kind)
        {
            case "team":
                var team = new CassieTeamDirectoryReader(database);
                return new PagingScenario(team, "TeamDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await team.ApplyAsync(RbacFacts.TeamDefinedFact(tenant, ids[index], names[index]));
                }, async cursor =>
                {
                    var page = await team.ListAsync(tenant, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.TeamId.ToString()).ToArray(), page.NextCursor);
                }, Expected(ids.Select(static id => id.ToString()).ToArray(), names, names.Select(static name => name.ToUpperInvariant()).ToArray(), descending, search));
            case "role":
                var role = new CassieRoleDirectoryReader(database);
                return new PagingScenario(role, "RoleDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await role.ApplyAsync(RbacFacts.RoleDefinedFact(tenant, ids[index], names[index]));
                }, async cursor =>
                {
                    var page = await role.ListAsync(tenant, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.RoleId.ToString()).ToArray(), page.NextCursor);
                }, Expected(ids.Select(static id => id.ToString()).ToArray(), names, names.Select(static name => name.ToUpperInvariant()).ToArray(), descending, search));
            case "member":
                var member = new CassieTeamMemberDirectoryReader(database, new InMemoryEventStore());
                return new PagingScenario(member, "TeamMemberDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await member.ApplyAsync(TeamMemberFacts.Assigned(tenant, parent, ids[index]));
                }, async cursor =>
                {
                    var page = await member.ListAsync(tenant, parent, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.MemberId.ToString()).ToArray(), page.NextCursor);
                }, Expected(ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), descending, search));
            case "permission":
                var permission = new CassieRolePermissionDirectoryReader(database);
                return new PagingScenario(permission, "RolePermissionDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await permission.ApplyAsync(RbacPermissionFacts.Assigned(tenant, parent, permissions[index]));
                }, async cursor =>
                {
                    var page = await permission.ListAsync(tenant, parent, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.Permission).ToArray(), page.NextCursor);
                }, Expected(permissions, permissions, permissions, descending, search));
            case "role_team":
                var roleTeams = new CassieRoleTeamDirectoryReader(database);
                return new PagingScenario(roleTeams, "RoleTeamDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await roleTeams.ApplyAsync(RbacTeamRoleFacts.Assigned(tenant, ids[index], parent));
                }, async cursor =>
                {
                    var page = await roleTeams.ListAsync(tenant, parent, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.TeamId.ToString()).ToArray(), page.NextCursor);
                }, Expected(ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), descending, search));
            case "team_role":
                var teamRoles = new CassieTeamRoleDirectoryReader(database);
                return new PagingScenario(teamRoles, "TeamRoleDirectory", async () =>
                {
                    for (var index = 0; index < ids.Length; index++)
                        await teamRoles.ApplyAsync(RbacTeamRoleFacts.Assigned(tenant, parent, ids[index]));
                }, async cursor =>
                {
                    var page = await teamRoles.ListAsync(tenant, parent, 1, cursor, search, descending);
                    return new Page<string>(page.Items.Select(static item => item.RoleId.ToString()).ToArray(), page.NextCursor);
                }, Expected(ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), ids.Select(static id => id.ToString()).ToArray(), descending, search));
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    static string[] Expected(string[] ids, string[] values, string[] sorts, bool descending, string? search)
    {
        var rows = Enumerable.Range(0, ids.Length).Where(index => search is null || values[index].Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(index => sorts[index], StringComparer.Ordinal).ThenBy(index => ids[index], StringComparer.Ordinal)
            .Select(index => ids[index]);
        return (descending ? rows.Reverse() : rows).ToArray();
    }

    sealed class PagingScenario(CassieProjectionRepository store, string component, Func<ValueTask> seed,
        Func<string?, ValueTask<Page<string>>> list, IReadOnlyList<string> expected) : IAsyncDisposable
    {
        public CassieProjectionRepository Store { get; } = store;
        public string Component { get; } = component;
        public Func<ValueTask> Seed { get; } = seed;
        public Func<string?, ValueTask<Page<string>>> List { get; } = list;
        public IReadOnlyList<string> Expected { get; } = expected;
        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
