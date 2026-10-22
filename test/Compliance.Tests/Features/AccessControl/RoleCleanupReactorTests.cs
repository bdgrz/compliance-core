using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveRelationshipsGivenDeletedRole()
    {
        // Arrange
        var firstTeam = Uuid.CreateVersion4();
        var secondTeam = Uuid.CreateVersion4();
        var permissions = new FakeRolePermissionDirectoryReader(
            new RolePermissionView(RoleId, "controls.read", Uuid.CreateVersion4(), 1), new RolePermissionView(RoleId, "controls.manage", Uuid.CreateVersion4(), 1));
        var teams = new FakeRoleTeamDirectoryReader(
            new RoleTeamView(RoleId, firstTeam), new RoleTeamView(RoleId, secondTeam));
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));

        // Act
        await scenario.RunAsync(new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, permissions, teams));

        // Assert
        Assert.Equal(4, scenario.SentRequests.Count);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.read");
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveRolePermission removal && removal.Permission == "controls.manage");
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamRole removal && removal.TeamId == firstTeam);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamRole removal && removal.TeamId == secondTeam);
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyRole()
    {
        // Arrange
        var permissions = new FakeRolePermissionDirectoryReader();
        var teams = new FakeRoleTeamDirectoryReader();
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));

        // Act
        await scenario.RunAsync(new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, permissions, teams));

        // Assert
        Assert.Equal([RoleId], permissions.QueriedRoles);
        Assert.Equal([RoleId], teams.QueriedRoles);
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldBindRemovalToTheDiscoveredEpisodeGivenCleanupReplay()
    {
        // Arrange
        var row = new RolePermissionView(RoleId, "controls.read", Uuid.CreateVersion4(), 5);
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));
        var reactor = new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            new FakeRolePermissionDirectoryReader(row), new FakeRoleTeamDirectoryReader());
        await scenario.RunAsync(reactor);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        var first = Assert.IsType<RemoveRolePermission>(scenario.SentRequests[0]);
        Assert.Equal(first, scenario.SentRequests[1]);
        Assert.Equal(row.AssignmentId, first.AssignmentId);
        Assert.Equal(row.Revision, first.ExpectedRevision);
        Assert.NotEqual(Uuid.Empty, first.CommandId);
    }

    [Fact]
    public async Task ShouldRetainTheCheckpointGivenAForeignPermissionRow()
    {
        // Arrange
        var scenario = new ReactorScenario().Given(new RoleDeleted(TenantId, RoleId));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new RoleCleanupReactor(checkpoints, scenario.Requests,
            new FakeRolePermissionDirectoryReader(new RolePermissionView(Uuid.CreateVersion4(), "controls.read",
                Uuid.CreateVersion4(), 1)), new FakeRoleTeamDirectoryReader());

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.RunAsync(reactor));

        // Assert
        Assert.Empty(scenario.SentRequests);
        Assert.Equal(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    sealed class FakeRolePermissionDirectoryReader(params RolePermissionView[] items) : IRolePermissionDirectoryReader
    {
        public List<Uuid> QueriedRoles { get; } = [];

        public ValueTask<Page<RolePermissionView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            Record(roleId, new Page<RolePermissionView>(items, null));

        ValueTask<Page<RolePermissionView>> Record(Uuid roleId, Page<RolePermissionView> page)
        {
            QueriedRoles.Add(roleId);
            return ValueTask.FromResult(page);
        }
    }

    sealed class FakeRoleTeamDirectoryReader(params RoleTeamView[] items) : IRoleTeamDirectoryReader
    {
        public List<Uuid> QueriedRoles { get; } = [];

        public ValueTask<Page<RoleTeamView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            Record(roleId, new Page<RoleTeamView>(items, null));

        ValueTask<Page<RoleTeamView>> Record(Uuid roleId, Page<RoleTeamView> page)
        {
            QueriedRoles.Add(roleId);
            return ValueTask.FromResult(page);
        }
    }
}
