using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AttestClientRbacManagementWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldDenyClientTeamAdministrationGivenActualAttestHistory(bool revoked, bool mcp)
    {
        // Arrange
        await using var provider = Compose();
        var teamId = Uuid.CreateVersion4();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: revoked);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DefineTeam(Tenant, teamId, "Client operations"), Context(mcp), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Team(Tenant, teamId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.False(retained.IsActive);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveAdministrationReadsButDenyTeamDeletionGivenHistoricalAttestAssignment()
    {
        // Arrange
        await using var provider = Compose();
        var teamId = Uuid.CreateVersion4();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var created = await bus.DispatchAsync(new DefineTeam(Tenant, teamId, "Client operations"), Context(), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var deleted = await bus.DispatchAsync(new DeleteTeam(Uuid.CreateVersion4(), Tenant, teamId), Context(), CancellationToken.None);
        var read = await bus.DispatchAsync(new GetTenantMember(Tenant, User), Context(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Team(Tenant, teamId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, deleted.Error?.Kind);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(User, read.Value!.UserId);
        Assert.True(retained.IsActive);
        Assert.Equal(1UL, retained.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPermitClientTeamAdministrationGivenNoSameClientActualAttestHistory(string practice, bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        var teamId = Uuid.CreateVersion4();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(), User, practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DefineTeam(Tenant, teamId, "Client operations"), Context(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Team(Tenant, teamId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retained.IsActive);
    }

    [Fact]
    public async Task ShouldPreserveTrustedSystemTeamBootstrapGivenProductionComposition()
    {
        // Arrange
        await using var provider = Compose();
        var teamId = Uuid.CreateVersion4();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new DefineTeam(Tenant, teamId, "System team"), RequestActor.System);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Team(Tenant, teamId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retained.IsActive);
    }

    [Theory]
    [InlineData("wrong_user")]
    [InlineData("wrong_tenant")]
    public async Task ShouldDenyClientAdministrationGivenMismatchedReturnedMembershipIdentity(string identity)
    {
        // Arrange
        await using var provider = Compose(identity: identity);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DefineTeam(Tenant, Uuid.CreateVersion4(), "Client operations"), Context(), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyClientAdministrationGivenNoOrdinaryGrantAndNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose(allowed: false);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DefineTeam(Tenant, Uuid.CreateVersion4(), "Client operations"), Context(), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    static RequestDispatchContext Context(bool mcp = false) => new(ProgramManagementServices.Actor(User),
        mcp ? new McpInvocation("synthetic.rbac") : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));

    [Fact]
    public async Task ShouldDenyAccessGrantRevocationGivenTrustedSystemActor()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new RevokeAccessGrant(Tenant, Uuid.CreateVersion4()), RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("authenticated member", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPreserveFixedRoleCatalogDenialGivenOrdinaryClientAdministrationGrant()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DefineRole(Tenant, Uuid.CreateVersion4(), "Custom role"), Context(), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("fixed by the system", result.Error!.Message, StringComparison.Ordinal);
    }

    static ServiceProvider Compose(bool allowed = true, string identity = "valid")
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(identity));
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(allowed)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships(string identity) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(identity == "wrong_user" ? Uuid.CreateVersion4() : User, identity == "wrong_tenant" ? Uuid.CreateVersion4() : Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
