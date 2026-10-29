using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

public sealed class AttestRetentionManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public async Task ShouldDenyRetentionManagementButPreserveReadsGivenActualHistoricalAttestAssignment(string operation)
    {
        // Arrange
        await using var provider = Compose();
        var id = await SeedEvidenceAsync(provider);
        var hold = Uuid.CreateVersion4();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        if (operation == "release")
        {
            var initial = await bus.DispatchAsync(new PlaceArtifactLegalHold(Tenant, "evidence_artifact", id,
                Sha, hold, 0, "Preserve original source"), Http(), CancellationToken.None);
            Assert.True(initial.IsSuccess, initial.Error?.Message);
        }
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetentionBasis(new(Tenant, "evidence_artifact", id, Sha)));
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var result = operation switch
        {
            "basis" => await bus.DispatchAsync(new RecordArtifactRetentionBasis(Uuid.CreateVersion4(), Tenant, "evidence_artifact",
                id, Sha, 0, "Native evidence period"), Http(), CancellationToken.None),
            "place" => await bus.DispatchAsync(new PlaceArtifactLegalHold(Tenant, "evidence_artifact", id,
                Sha, hold, 0, "Preserve original source"), Http(), CancellationToken.None),
            _ => await bus.DispatchAsync(new ReleaseArtifactLegalHold(Uuid.CreateVersion4(), Tenant, "evidence_artifact", id,
                Sha, hold, 1, "Client hold release"), Http(), CancellationToken.None)
        };
        var read = await bus.DispatchAsync(new GetArtifactRetention(Tenant, "evidence_artifact", id),
            new RequestDispatchContext(ProgramManagementServices.Actor(User), new McpInvocation("synthetic.retention.read")),
            CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetentionBasis(new(Tenant, "evidence_artifact", id, Sha)));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(before.Revision, retained.Revision);
        Assert.False(read.Value!.DispositionAllowed);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldRetainNativeRetentionBasisGivenHistoryOutsideTheClientAttestWall(string practice, bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        var id = await SeedEvidenceAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(), User,
            practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordArtifactRetentionBasis(Uuid.CreateVersion4(), Tenant, "evidence_artifact", id, Sha, 0, "Native evidence period"),
            Http(), CancellationToken.None);
        var read = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new GetArtifactRetention(Tenant, "evidence_artifact", id), Http(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetentionBasis(new(Tenant, "evidence_artifact", id, Sha)));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, retained.Revision);
        Assert.NotNull(retained.Basis);
        Assert.Equal(new DateOnly(2019, 1, 1), retained.Basis.PeriodStart);
        Assert.Equal(new DateOnly(2019, 12, 31), retained.Basis.PeriodEnd);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(retained.Basis.PeriodStart, read.Value!.PeriodStart);
        Assert.Equal(retained.Basis.PeriodEnd, read.Value.PeriodEnd);
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldPreserveOrdinaryAuthorityAndPersonalTransportGivenNoAttestHistory(string refusal)
    {
        // Arrange
        await using var provider = Compose(allowed: refusal != "grant");
        var id = await SeedEvidenceAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var context = refusal == "grant" ? Http() : new RequestDispatchContext(ProgramManagementServices.Actor(User),
            refusal == "mcp" ? new McpInvocation("synthetic.retention.decision") : new DirectInvocation());

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordArtifactRetentionBasis(Uuid.CreateVersion4(), Tenant, "evidence_artifact", id, Sha, 0, "Native period"),
            context, CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetentionBasis(new(Tenant, "evidence_artifact", id, Sha)));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, retained.Revision);
    }

    static RequestDispatchContext Http() => new(ProgramManagementServices.Actor(User),
        new HttpInvocation("POST", "/synthetic/retention", "/synthetic/retention", "synthetic"));

    static async Task<Uuid> SeedEvidenceAsync(IServiceProvider provider)
    {
        var id = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new EvidenceArtifact(Tenant, id), artifact =>
        {
            var registered = artifact.Register(new("Payroll access", null, "export", "manual", DateTimeOffset.UtcNow,
                new(2019, 1, 1), new(2019, 12, 31), "confidential"), Sha, 100,
                ActorReference.ForMember(RbacIds.Member(Tenant, User), "Collector"), DateTimeOffset.UtcNow);
            Assert.True(registered.IsSuccess);
            return registered;
        });
        return id;
    }

    static ServiceProvider Compose(bool allowed = true)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenant == Tenant.ToString() && userId == User
                ? new TenantMembershipView(User, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
