using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class AccessCassieCompositionTests
{
    static readonly string[] Projectors =
    [
        "PermissionProjection", "TeamDirectory", "TeamMemberDirectory", "RoleDirectory",
        "RolePermissionDirectory", "RoleTeamDirectory", "TeamRoleDirectory"
    ];

    [Fact]
    public async Task ShouldReconstituteTenantIsolatedAccessGivenRegisteredNativeProjectorReplay()
    {
        // Arrange
        var events = new InMemoryEventStore();
        var tenant = Uuid.CreateVersion4();
        var otherTenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        await SeedAsync(events, tenant, user, team, "First tenant team");
        await SeedAsync(events, otherTenant, user, team, "Second tenant team");
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        string[] original;
        string[] otherOriginal;

        // Act
        await using (var provider = CreateProvider(events, database.ConnectionString))
        {
            await CatchUpAsync(provider, tenant);
            await CatchUpAsync(provider, otherTenant);
            original = await ReadRowsAsync(provider, tenant, user, team);
            otherOriginal = await ReadRowsAsync(provider, otherTenant, user, team);
        }
        await using var reopened = CreateProvider(events, database.ConnectionString);
        var persisted = await ReadRowsAsync(reopened, tenant, user, team);
        var otherPersisted = await ReadRowsAsync(reopened, otherTenant, user, team);
        await using var replayDatabase = await CassieRepositoryTestDatabase.OpenAsync();
        await using var replay = CreateProvider(events, replayDatabase.ConnectionString);
        await using (var scope = replay.CreateAsyncScope())
        {
            Assert.Empty((await scope.ServiceProvider.GetRequiredService<ITeamDirectoryReader>()
                .ListAsync(tenant, 50, null, null, false)).Items);
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<IMemberAccessReader>()
                .ReadAsync(tenant, RbacIds.Member(tenant, user)));
            foreach (var name in Projectors)
                Assert.Equal(ProjectionCheckpoint.Start, await Store(scope.ServiceProvider, name)
                    .LoadCheckpointAsync(new CheckpointIdentity(name, EventStreamPattern.ForPattern(tenant.ToString()))));
        }
        await CatchUpAsync(replay, tenant);
        await CatchUpAsync(replay, otherTenant);
        var reconstructed = await ReadRowsAsync(replay, tenant, user, team);
        var otherReconstructed = await ReadRowsAsync(replay, otherTenant, user, team);

        // Assert
        Assert.NotEqual(database.ConnectionString, replayDatabase.ConnectionString);
        Assert.Equal(ExpectedRows(tenant, user, team, "First tenant team"), original);
        Assert.Equal(ExpectedRows(otherTenant, user, team, "Second tenant team"), otherOriginal);
        Assert.Equal(original, persisted);
        Assert.Equal(otherOriginal, otherPersisted);
        Assert.Equal(original, reconstructed);
        Assert.Equal(otherOriginal, otherReconstructed);
    }

    static ServiceProvider CreateProvider(InMemoryEventStore events, string connectionString)
    {
        var services = new ServiceCollection();
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
            ["Cassie:ConnectionString"] = connectionString
        }).Build(), developerAuthentication: true);
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task SeedAsync(InMemoryEventStore events, Uuid tenant, Uuid user, Uuid team, string name)
    {
        var member = RbacIds.Member(tenant, user);
        var role = BuiltInRbac.ViewerRoleId(tenant);
        var episode = Uuid.CreateVersion4();
        await AppendAsync(new Member(tenant, user), MemberFacts.Registered(tenant, member, user,
            membershipEpisodeId: episode));
        await AppendAsync(new Team(tenant, team), RbacFacts.TeamDefinedFact(tenant, team, name));
        await AppendAsync(new Role(tenant, role), RbacFacts.RoleDefinedFact(tenant, role, "Viewer"));
        await AppendAsync(new TeamMember(tenant, team, member), TeamMemberFacts.Assigned(tenant, team, member, episode));
        await AppendAsync(new TeamRole(tenant, team, role), RbacTeamRoleFacts.Assigned(tenant, team, role));
        await AppendAsync(new RolePermission(tenant, role, RbacPermissions.TenantAccess),
            RbacPermissionFacts.Assigned(tenant, role, RbacPermissions.TenantAccess));

        async Task AppendAsync(Aggregate owner, DomainEvent fact)
        {
            fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1,
                new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)));
            await events.AppendAsync(owner.Stream, 0, [fact]);
        }
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenant)
    {
        foreach (var name in Projectors)
        {
            await using var scope = provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
            Assert.Equal(WorkloadScope.PerTenant, registration.Scope);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            var store = Store(services, name);
            await new ProjectorScenario(new TenantId(tenant.ToString())).RunAsync(projector);
            var identity = new CheckpointIdentity(name, projector.Pattern);
            var checkpoint = await store.LoadCheckpointAsync(identity);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
            Assert.NotEqual(ProjectionCheckpoint.Start, checkpoint);
            Assert.Equal(checkpoint, await store.LoadCheckpointAsync(identity));
            Assert.Equal(checkpoint, await runner.RunAsync(projector, checkpoint));
        }
    }

    static CassieProjectionRepository Store(IServiceProvider services, string name) => name switch
    {
        "PermissionProjection" => services.GetRequiredService<CassiePermissionAuthorizer>(),
        "TeamDirectory" => services.GetRequiredService<CassieTeamDirectoryReader>(),
        "TeamMemberDirectory" => services.GetRequiredService<CassieTeamMemberDirectoryReader>(),
        "RoleDirectory" => services.GetRequiredService<CassieRoleDirectoryReader>(),
        "RolePermissionDirectory" => services.GetRequiredService<CassieRolePermissionDirectoryReader>(),
        "RoleTeamDirectory" => services.GetRequiredService<CassieRoleTeamDirectoryReader>(),
        "TeamRoleDirectory" => services.GetRequiredService<CassieTeamRoleDirectoryReader>(),
        _ => throw new InvalidOperationException("Unknown access projector.")
    };

    static async Task<string[]> ReadRowsAsync(IServiceProvider provider, Uuid tenant, Uuid user, Uuid team)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var role = BuiltInRbac.ViewerRoleId(tenant);
        var teams = services.GetRequiredService<ITeamDirectoryReader>();
        var members = services.GetRequiredService<ITeamMemberDirectoryReader>();
        var roles = services.GetRequiredService<IRoleDirectoryReader>();
        var permissions = services.GetRequiredService<IRolePermissionDirectoryReader>();
        var roleTeams = services.GetRequiredService<IRoleTeamDirectoryReader>();
        var teamRoles = services.GetRequiredService<ITeamRoleDirectoryReader>();
        var access = services.GetRequiredService<IMemberAccessReader>();
        Assert.Same(Store(services, "TeamDirectory"), teams);
        Assert.Same(Store(services, "TeamMemberDirectory"), members);
        Assert.Same(Store(services, "RoleDirectory"), roles);
        Assert.Same(Store(services, "RolePermissionDirectory"), permissions);
        Assert.Same(Store(services, "RoleTeamDirectory"), roleTeams);
        Assert.Same(Store(services, "TeamRoleDirectory"), teamRoles);
        Assert.Same(Store(services, "PermissionProjection"), access);
        Assert.Same(access, services.GetRequiredService<IPermissionAuthorizer>());
        var teamRow = Assert.Single((await teams.ListAsync(tenant, 50, null, null, false)).Items);
        var memberRow = Assert.Single((await members.ListAsync(tenant, team, 50, null, null, false)).Items);
        var roleRow = Assert.Single((await roles.ListAsync(tenant, 50, null, null, false)).Items);
        var permissionRow = Assert.Single((await permissions.ListAsync(tenant, role, 50, null, null, false)).Items);
        var roleTeamRow = Assert.Single((await roleTeams.ListAsync(tenant, role, 50, null, null, false)).Items);
        var teamRoleRow = Assert.Single((await teamRoles.ListAsync(tenant, team, 50, null, null, false)).Items);
        var accessRow = Assert.Single(await access.ReadAsync(tenant, RbacIds.Member(tenant, user)));
        return
        [
            $"team:{teamRow.TeamId}:{teamRow.Name}",
            $"member:{memberRow.TeamId}:{memberRow.MemberId}",
            $"role:{roleRow.RoleId}:{roleRow.Name}",
            $"permission:{permissionRow.RoleId}:{permissionRow.Permission}",
            $"role_team:{roleTeamRow.RoleId}:{roleTeamRow.TeamId}",
            $"team_role:{teamRoleRow.TeamId}:{teamRoleRow.RoleId}",
            $"access:{accessRow.TeamId}:{accessRow.RoleId}:{Assert.Single(accessRow.Permissions)}"
        ];
    }

    static string[] ExpectedRows(Uuid tenant, Uuid user, Uuid team, string name)
    {
        var role = BuiltInRbac.ViewerRoleId(tenant);
        return
        [
            $"team:{team}:{name}", $"member:{team}:{RbacIds.Member(tenant, user)}", $"role:{role}:Viewer",
            $"permission:{role}:{RbacPermissions.TenantAccess}", $"role_team:{role}:{team}",
            $"team_role:{team}:{role}", $"access:{team}:{role}:{RbacPermissions.TenantAccess}"
        ];
    }
}
