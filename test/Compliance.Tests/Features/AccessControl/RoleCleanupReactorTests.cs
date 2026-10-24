using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveEveryRelationshipGivenDeletedRoleAndNoProjection()
    {
        // Arrange
        await using var provider = Build();
        await DefineAndDeleteAsync(provider, [], RoleId);
        var permissions = Enumerable.Range(0, 205).Select(index => $"controls.item{index:000}").ToArray();
        var teams = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        foreach (var permission in permissions)
            await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, permission);
        foreach (var team in teams)
            await RbacTeamRoleFacts.GrantAsync(provider, TenantId, team, RoleId);

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        foreach (var permission in permissions)
            Assert.False((await CleanupReactorHost.ReadAsync(provider,
                new RolePermission(TenantId, RoleId, permission))).IsAssigned);
        foreach (var team in teams)
            Assert.False((await CleanupReactorHost.ReadAsync(provider, new TeamRole(TenantId, team, RoleId))).IsAssigned);
    }

    [Fact]
    public async Task ShouldRemoveRelationshipsGivenDeletedRoleWhileTheProjectionIsEmpty()
    {
        // Arrange
        await using var provider = Build();
        await DefineAndDeleteAsync(provider, [], RoleId);
        await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, "controls.read");
        var team = Uuid.CreateVersion4();
        await RbacTeamRoleFacts.GrantAsync(provider, TenantId, team, RoleId);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(await DeletedFactAsync(provider));

        // Act
        await RunWithRecordingBusAsync(provider, scenario);

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count);
        var permission = Assert.Single(scenario.SentRequests.OfType<RemoveRolePermission>());
        Assert.Equal("controls.read", permission.Permission);
        Assert.Equal((await CleanupReactorHost.ReadAsync(provider, new RolePermission(TenantId, RoleId, "controls.read")))
            .AssignmentId, permission.AssignmentId);
        var teamRole = Assert.Single(scenario.SentRequests.OfType<RemoveTeamRole>());
        Assert.Equal(team, teamRole.TeamId);
    }

    [Fact]
    public async Task ShouldBindRemovalToTheDiscoveredEpisodeGivenCleanupReplay()
    {
        // Arrange
        await using var provider = Build();
        await DefineAndDeleteAsync(provider, [], RoleId);
        await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, "controls.read");
        await RbacTeamRoleFacts.GrantAsync(provider, TenantId, Uuid.CreateVersion4(), RoleId);
        var trigger = await DeletedFactAsync(provider);
        var first = new ReactorScenario(new TenantId(TenantId.ToString())).Given(trigger);
        var replay = new ReactorScenario(new TenantId(TenantId.ToString())).Given(trigger);

        // Act
        await RunWithRecordingBusAsync(provider, first);
        await RunWithRecordingBusAsync(provider, replay);

        // Assert
        var permission = await CleanupReactorHost.ReadAsync(provider, new RolePermission(TenantId, RoleId, "controls.read"));
        Assert.Equal(first.SentRequests.Count, replay.SentRequests.Count);
        var removal = Assert.IsType<RemoveRolePermission>(first.SentRequests.First(static request => request is RemoveRolePermission));
        Assert.Equal(removal, replay.SentRequests.First(static request => request is RemoveRolePermission));
        Assert.Equal(permission.AssignmentId, removal.AssignmentId);
        Assert.Equal(permission.Version, removal.ExpectedRevision);
        Assert.NotEqual(Uuid.Empty, removal.CommandId);
        var teamRemoval = Assert.IsType<RemoveTeamRole>(first.SentRequests.First(static request => request is RemoveTeamRole));
        Assert.Equal(teamRemoval, replay.SentRequests.First(static request => request is RemoveTeamRole));
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyRole()
    {
        // Arrange
        await using var provider = Build();
        await DefineAndDeleteAsync(provider, [], RoleId);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(await DeletedFactAsync(provider));

        // Act
        await RunWithRecordingBusAsync(provider, scenario);

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldIgnoreRelationshipsGivenOtherRolesAndAlreadyRemovedOnes()
    {
        // Arrange
        await using var provider = Build();
        var other = Uuid.CreateVersion4();
        await DefineAndDeleteAsync(provider, [other], RoleId);
        await RbacPermissionFacts.GrantAsync(provider, TenantId, other, "controls.read");
        await RbacTeamRoleFacts.GrantAsync(provider, TenantId, Uuid.CreateVersion4(), other);
        await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, "controls.write");
        await RbacPermissionFacts.RevokeAsync(provider, TenantId, RoleId, "controls.write");

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        Assert.True((await CleanupReactorHost.ReadAsync(provider, new RolePermission(TenantId, other, "controls.read"))).IsAssigned);
        var records = await RecordsAsync(provider, "rbac-role-permissions");
        Assert.Single(records.Select(static record => record.Event).OfType<RolePermissionRemoved>(),
            removed => removed.RoleId == RoleId);
    }

    [Fact]
    public async Task ShouldKeepTheNewerPermissionEpisodeGivenReassignmentAfterTheScan()
    {
        // Arrange
        var store = new ScanHookEventStore();
        await using var provider = Build(store);
        await DefineAndDeleteAsync(provider, [], RoleId);
        await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, "controls.read");
        var old = (await CleanupReactorHost.ReadAsync(provider, new RolePermission(TenantId, RoleId, "controls.read")))
            .AssignmentId;
        store.AfterNextScan(EventStreamPattern.ForPattern(TenantId.ToString(), "rbac-role-permissions"), async () =>
        {
            await RbacPermissionFacts.RevokeAsync(provider, TenantId, RoleId, "controls.read");
            await RbacPermissionFacts.GrantAsync(provider, TenantId, RoleId, "controls.read");
        });

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        var current = await CleanupReactorHost.ReadAsync(provider, new RolePermission(TenantId, RoleId, "controls.read"));
        Assert.True(current.IsAssigned);
        Assert.NotEqual(old, current.AssignmentId);
    }

    [Fact]
    public async Task ShouldKeepTheNewerTeamRoleEpisodeGivenReassignmentAfterTheScan()
    {
        // Arrange
        var store = new ScanHookEventStore();
        await using var provider = Build(store);
        await DefineAndDeleteAsync(provider, [], RoleId);
        var team = Uuid.CreateVersion4();
        await RbacTeamRoleFacts.GrantAsync(provider, TenantId, team, RoleId);
        var old = (await CleanupReactorHost.ReadAsync(provider, new TeamRole(TenantId, team, RoleId))).AssignmentId;
        store.AfterNextScan(EventStreamPattern.ForPattern(TenantId.ToString(), "rbac-team-roles"), async () =>
        {
            await RbacTeamRoleFacts.RevokeAsync(provider, TenantId, team, RoleId);
            await RbacTeamRoleFacts.GrantAsync(provider, TenantId, team, RoleId);
        });

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        var current = await CleanupReactorHost.ReadAsync(provider, new TeamRole(TenantId, team, RoleId));
        Assert.True(current.IsAssigned);
        Assert.NotEqual(old, current.AssignmentId);
    }

    static Reactor Create(IProjectionCheckpointStore checkpoints, IRequestBus bus, IDomainEventReader events,
        IAggregateReader reader) => new RoleCleanupReactor(checkpoints, bus, events, reader);

    static ServiceProvider Build(IEventStore? store = null) => RbacManagementServices.Build(allowed: true,
        portia => portia.AddRequestHandler<RemoveRolePermissionHandler>().AddRequestHandler<RemoveTeamRoleHandler>(),
        store);

    static async Task DefineAndDeleteAsync(IServiceProvider provider, Uuid[] otherRoles, Uuid roleId)
    {
        await CleanupReactorHost.SeedAsync(provider, new Role(TenantId, roleId), role => role.Define("Reviewer"));
        foreach (var other in otherRoles)
            await CleanupReactorHost.SeedAsync(provider, new Role(TenantId, other), role => role.Define("Other"));
        await CleanupReactorHost.SeedAsync(provider, new Role(TenantId, roleId), role => role.Delete());
    }

    static async Task<RoleDeleted> DeletedFactAsync(IServiceProvider provider) =>
        Assert.Single((await RecordsAsync(provider, "rbac-roles")).Select(static record => record.Event)
            .OfType<RoleDeleted>());

    static async Task<List<DomainEventRecord>> RecordsAsync(IServiceProvider provider, string area)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in provider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString(), area), EventCursor.Start,
                               CancellationToken.None))
            records.Add(record);
        return records;
    }

    static async Task RunWithRecordingBusAsync(ServiceProvider provider, ReactorScenario scenario)
    {
        await using var scope = provider.CreateAsyncScope();
        await scenario.RunAsync(new RoleCleanupReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>()));
    }
}
