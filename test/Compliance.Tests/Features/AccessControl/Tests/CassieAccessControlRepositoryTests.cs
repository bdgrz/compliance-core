using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieAccessControlRepositoryTests
{
    [Theory]
    [InlineData("team")]
    [InlineData("member")]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("role_team")]
    [InlineData("team_role")]
    [InlineData("authorization")]
    public async Task ShouldPersistRowsAndProgressGivenCommittedCassieBatch(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var checkpoint = new ProjectionCheckpoint(new EventCursor("access-progress-1"));
        await using (var writer = Create(database.Database, kind, tenant, id))
        {
            await using var batch = await writer.Store.BeginAsync(new ProjectionBatchContext(writer.Identity, ProjectionCheckpoint.Start));
            await writer.Apply();
            await batch.CommitAsync(checkpoint);
        }

        // Act
        await using var reopened = Create(database.Database, kind, tenant, id);
        var found = await reopened.Exists(tenant);
        var otherTenant = await reopened.Exists(Uuid.CreateVersion4());
        var progress = await reopened.Store.LoadCheckpointAsync(reopened.Identity);

        // Assert
        Assert.True(found);
        Assert.False(otherTenant);
        Assert.Equal(checkpoint, progress);
    }

    [Theory]
    [InlineData("team")]
    [InlineData("member")]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("role_team")]
    [InlineData("team_role")]
    [InlineData("authorization")]
    public async Task ShouldDiscardRowsAndProgressGivenUncommittedCassieBatch(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, Uuid.CreateVersion4());

        // Act
        await using (await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
            await scenario.Apply();

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
    }

    [Theory]
    [InlineData("team")]
    [InlineData("member")]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("role_team")]
    [InlineData("team_role")]
    [InlineData("authorization")]
    public async Task ShouldKeepRebuildRowsOutOfLiveQueriesGivenCandidateGeneration(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, Uuid.CreateVersion4());
        var candidate = new CheckpointIdentity(scenario.Identity.ComponentName, scenario.Identity.Pattern, "candidate");
        var checkpoint = new ProjectionCheckpoint(new EventCursor("access-rebuild-1"));

        // Act
        await using (var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(candidate, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
        Assert.Equal(checkpoint, await scenario.Store.LoadCheckpointAsync(candidate));
    }

    [Theory]
    [InlineData("team")]
    [InlineData("member")]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("role_team")]
    [InlineData("team_role")]
    [InlineData("authorization")]
    public async Task ShouldRefuseWithoutRowsGivenFactOutsideBoundBatchTenant(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, Uuid.CreateVersion4());
        var alien = new CheckpointIdentity(scenario.Identity.ComponentName, EventStreamPattern.ForPattern(Uuid.CreateVersion4().ToString()));

        // Act
        Exception? failure;
        await using (await scenario.Store.BeginAsync(new ProjectionBatchContext(alien, ProjectionCheckpoint.Start)))
            failure = await Record.ExceptionAsync(async () => await scenario.Apply());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(alien));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("direction")]
    [InlineData("search")]
    [InlineData("directory")]
    public async Task ShouldRejectCursorGivenDifferentDirectoryQuery(string change)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var teams = new CassieTeamDirectoryReader(database.Database);
        await using var roles = new CassieRoleDirectoryReader(database.Database);
        var tenant = Uuid.CreateVersion4();
        await using (var batch = await teams.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("TeamDirectory", EventStreamPattern.ForPattern(tenant.ToString())), ProjectionCheckpoint.Start)))
        {
            await teams.ApplyAsync(RbacFacts.TeamDefinedFact(tenant, Uuid.CreateVersion4(), "Alpha"));
            await teams.ApplyAsync(RbacFacts.TeamDefinedFact(tenant, Uuid.CreateVersion4(), "Beta"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var first = await teams.ListAsync(tenant, 1, null, null, false);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (change == "directory")
                await roles.ListAsync(tenant, 1, first.NextCursor, null, false);
            else
                await teams.ListAsync(change == "tenant" ? Uuid.CreateVersion4() : tenant,
                    1, first.NextCursor, change == "search" ? "a" : null, change == "direction");
        });

        // Assert
        Assert.NotNull(first.NextCursor);
        Assert.IsType<AccessControlDirectoryQueryException>(failure);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenDifferentParentRole()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var permissions = new CassieRolePermissionDirectoryReader(database.Database);
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        await using (var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(tenant.ToString())), ProjectionCheckpoint.Start)))
        {
            await permissions.ApplyAsync(RbacPermissionFacts.Assigned(tenant, role, "controls.manage"));
            await permissions.ApplyAsync(RbacPermissionFacts.Assigned(tenant, role, "controls.read"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var first = await permissions.ListAsync(tenant, role, 1, null, null, false);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await permissions.ListAsync(tenant, Uuid.CreateVersion4(), 1, first.NextCursor, null, false));

        // Assert
        Assert.NotNull(first.NextCursor);
        Assert.IsType<AccessControlDirectoryQueryException>(failure);
    }

    [Fact]
    public async Task ShouldRetainFullUnsignedRevisionGivenAssignmentBeyondSignedRange()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var permissions = new CassieRolePermissionDirectoryReader(database.Database);
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var assignment = Uuid.CreateVersion4();

        // Act
        await using (var batch = await permissions.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("RolePermissionDirectory", EventStreamPattern.ForPattern(tenant.ToString())), ProjectionCheckpoint.Start)))
        {
            await permissions.ApplyAsync(new RolePermissionAssigned(tenant, role, "controls.read", assignment,
                ulong.MaxValue - 1, RbacPermissionFacts.Actor));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var row = Assert.Single((await permissions.ListAsync(tenant, role, 1, null, null, false)).Items);

        // Assert
        Assert.Equal(assignment, row.AssignmentId);
        Assert.Equal(ulong.MaxValue, row.Revision);
    }

    static Scenario Create(CassieProjectionDatabase database, string kind, Uuid tenant, Uuid id)
    {
        switch (kind)
        {
            case "team":
                var teams = new CassieTeamDirectoryReader(database);
                return new Scenario(teams, "TeamDirectory", tenant,
                    () => teams.ApplyAsync(RbacFacts.TeamDefinedFact(tenant, id, "Team")),
                    async target => await teams.GetAsync(target, id) is not null);
            case "member":
                var members = new CassieTeamMemberDirectoryReader(database, new InMemoryEventStore());
                return new Scenario(members, "TeamMemberDirectory", tenant,
                    () => members.ApplyAsync(TeamMemberFacts.Assigned(tenant, id, id)),
                    async target => (await members.ListAsync(target, id, 50, null, null, false)).Items.Count != 0);
            case "role":
                var roles = new CassieRoleDirectoryReader(database);
                return new Scenario(roles, "RoleDirectory", tenant,
                    () => roles.ApplyAsync(RbacFacts.RoleDefinedFact(tenant, id, "Role")),
                    async target => await roles.GetAsync(target, id) is not null);
            case "permission":
                var permissions = new CassieRolePermissionDirectoryReader(database);
                return new Scenario(permissions, "RolePermissionDirectory", tenant,
                    () => permissions.ApplyAsync(RbacPermissionFacts.Assigned(tenant, id, RbacPermissions.TenantAccess)),
                    async target => (await permissions.ListAsync(target, id, 50, null, null, false)).Items.Count != 0);
            case "role_team":
                var roleTeams = new CassieRoleTeamDirectoryReader(database);
                return new Scenario(roleTeams, "RoleTeamDirectory", tenant,
                    () => roleTeams.ApplyAsync(RbacTeamRoleFacts.Assigned(tenant, id, id)),
                    async target => (await roleTeams.ListAsync(target, id, 50, null, null, false)).Items.Count != 0);
            case "team_role":
                var teamRoles = new CassieTeamRoleDirectoryReader(database);
                return new Scenario(teamRoles, "TeamRoleDirectory", tenant,
                    () => teamRoles.ApplyAsync(RbacTeamRoleFacts.Assigned(tenant, id, id)),
                    async target => (await teamRoles.ListAsync(target, id, 50, null, null, false)).Items.Count != 0);
            case "authorization":
                var authorizer = new CassiePermissionAuthorizer(database, new FixedMembershipDirectory(true),
                    new InMemoryEventStore(), new FixedMemberAccessEligibility(true));
                var member = RbacIds.Member(tenant, id);
                var team = BuiltInRbac.AdministratorsTeamId(tenant);
                var role = BuiltInRbac.TenantAdministrationRoleId(tenant);
                return new Scenario(authorizer, "PermissionProjection", tenant, async () =>
                {
                    await authorizer.ApplyAsync(MemberFacts.Registered(tenant, member, id));
                    await authorizer.ApplyAsync(RbacFacts.TeamDefinedFact(tenant, team, "Administrators"));
                    await authorizer.ApplyAsync(RbacFacts.RoleDefinedFact(tenant, role, "Org Admin"));
                    await authorizer.ApplyAsync(TeamMemberFacts.Assigned(tenant, team, member));
                    await authorizer.ApplyAsync(RbacTeamRoleFacts.Assigned(tenant, team, role));
                    await authorizer.ApplyAsync(RbacPermissionFacts.Assigned(tenant, role, RbacPermissions.TenantAccess));
                }, async target => (await authorizer.ReadAsync(target, member)).Count != 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    sealed class Scenario(CassieProjectionRepository store, string component, Uuid tenant,
        Func<ValueTask> apply, Func<Uuid, ValueTask<bool>> exists) : IAsyncDisposable
    {
        public CassieProjectionRepository Store { get; } = store;
        public CheckpointIdentity Identity { get; } = new(component, EventStreamPattern.ForPattern(tenant.ToString()));
        public Func<ValueTask> Apply { get; } = apply;
        public Func<Uuid, ValueTask<bool>> Exists { get; } = exists;
        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
