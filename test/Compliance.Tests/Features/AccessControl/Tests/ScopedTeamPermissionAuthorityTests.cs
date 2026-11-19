using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Cntryl.Portia.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ScopedTeamPermissionAuthorityTests
{
    [Fact]
    public async Task ShouldUseTeamOwnersGivenProductionPermissionComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://127.0.0.1:1/ws",
            ["Fitz:ApplicationName"] = "compliance",
            ["Cassie:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=postgres;Username=root;Password=test;SSL Mode=Disable",
        }).Build());
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        // Act
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionAuthorizer>();
        var programs = scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>();
        var roles = scope.ServiceProvider.GetRequiredService<IAccessGrantRolePermissionAuthorizer>();
        var resources = scope.ServiceProvider.GetRequiredService<IAccessGrantScopePermissionAuthorizer>();

        // Assert
        Assert.IsType<TeamRolePermissionAuthorizer>(permissions);
        Assert.Same(permissions, programs);
        Assert.Same(permissions, roles);
        Assert.Same(permissions, resources);
    }

    [Theory]
    [InlineData("client_personnel", "organization", true, true)]
    [InlineData("client_personnel", "program", false, true)]
    [InlineData("guest", "organization", false, true)]
    [InlineData("guest", "other_program", false, false)]
    public async Task ShouldApplyScopeAndGuestCapGivenAuthoritativeTeamRole(string affiliation,
        string kind, bool organizationExpected, bool programExpected)
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(true, portia => portia.AddRequestHandler<RegisterMemberHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenant, user);
        var team = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = new ActorAttribution("administrator", "bdgrz");
        await RbacManagementServices.SeedAsync(provider, new Tenant(tenant), owner =>
        {
            Assert.True(owner.Register(user, "Acme", "scope-test").IsSuccess);
            return owner.ConfirmSlug("scope-test");
        });
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new RegisterMember(Uuid.CreateVersion4(), tenant, user, affiliation,
                affiliation == "guest" ? new DateOnly(2027, 1, 1) : null,
                affiliation == "guest" ? [program] : null), RequestActor.System)).IsSuccess);
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var member = await reader.HydrateAsync(new Member(tenant, user));
        await RbacManagementServices.SeedAsync(provider, new Team(tenant, team), owner => owner.Define("Reviewers"));
        await RbacManagementServices.SeedAsync(provider, new Role(tenant, role), owner => owner.Define("Custom role"));
        await RbacManagementServices.SeedAsync(provider, new TeamMember(tenant, team, memberId), owner => owner.Assign(member.MembershipEpisodeId));
        var command = Uuid.CreateVersion4();
        var assignmentScope = new AccessGrantScope(kind == "organization" ? AccessGrantScopeKind.Organization : AccessGrantScopeKind.Program,
            kind == "organization" ? tenant : kind == "other_program" ? Uuid.CreateVersion4() : program);
        await RbacManagementServices.SeedAsync(provider, new TeamRole(tenant, team, role, command, assignmentScope), owner => owner.Assign(command, 0, actor));
        var permission = Uuid.CreateVersion4();
        await RbacManagementServices.SeedAsync(provider, new RolePermission(tenant, role, RbacPermissions.ProgramManage, permission),
            owner => owner.Assign(permission, 0, actor));
        var authorizer = new TeamRolePermissionAuthorizer(reader, scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), TimeProvider.System);

        // Act
        var organizationAllowed = await authorizer.IsAllowedAsync(tenant, user, memberId, RbacPermissions.ProgramManage);
        var programAllowed = await authorizer.IsAllowedAsync(tenant, user, memberId, program, RbacPermissions.ProgramManage);

        // Assert
        Assert.Equal(organizationExpected, organizationAllowed);
        Assert.Equal(programExpected, programAllowed);
    }
}
