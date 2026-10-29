using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AttestAccessReviewWriteWallTests
{
    [Theory]
    [InlineData("client_personnel", false)]
    [InlineData("client_personnel", true)]
    [InlineData("guest", false)]
    [InlineData("guest", true)]
    public async Task ShouldDenyPopulationManagementGivenActualAttestHistoryAndRelinkedCurrentMembership(string affiliation, bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(affiliation);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ManagerUserId);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open(), mcp);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Contains("Attest", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldDenyPopulationManagementGivenRevokedActualAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ManagerUserId, revoked: true);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRetainPopulationAndAllowReadGivenOrdinaryClientManagementAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());
        var read = await fixture.SendAsync(fixture.Source.ManagerUserId,
            new GetAccessPopulation(fixture.Source.TenantId, result.Value!.PopulationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(result.Value.PopulationId, read.Value!.PopulationId);
    }

    [Fact]
    public async Task ShouldPermitClientManagementGivenSameCanonicalUserAttestHistoryInDifferentTenant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, Uuid.CreateVersion4(), fixture.Source.ManagerUserId);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldPermitClientManagementGivenActualAdvisoryHistoryForSameClient()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId,
            fixture.Source.ManagerUserId, practice: "advisory");

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRetainPersonalAccessDecisionGivenOrdinaryAssignedReviewerWithoutAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var (populationId, _) = await fixture.Source.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.Source.ClassifyStandardAsync(populationId);
        var launched = await fixture.Source.LaunchAsync(populationId);
        var campaign = await fixture.Source.CampaignAsync(launched.CampaignId);
        var itemId = AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy");

        // Act
        var success = (await fixture.SendAsync(fixture.Source.ReviewerUserId,
            fixture.Source.Decision(launched.CampaignId, itemId, 1, "keep", "Reviewed"))).IsSuccess;
        var retained = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new GetAccessReviewCampaign(fixture.Source.TenantId, launched.CampaignId));

        // Assert
        Assert.True(success);
        Assert.True(retained.IsSuccess);
        Assert.Equal("decided", Assert.Single(retained.Value!.Items, item => item.Item.ItemId == itemId).Status);
    }

    [Fact]
    public async Task ShouldDenyAssignedAccessDecisionGivenActualAttestHistoryDespitePersonalHttpAndReviewAssignment()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var (populationId, _) = await fixture.Source.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.Source.ClassifyStandardAsync(populationId);
        var launched = await fixture.Source.LaunchAsync(populationId);
        var campaign = await fixture.Source.CampaignAsync(launched.CampaignId);
        var itemId = AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy");
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ReviewerUserId);
        var before = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new GetAccessReviewCampaign(fixture.Source.TenantId, launched.CampaignId));
        var preview = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new PreviewBulkAccessDecision(fixture.Source.TenantId, launched.CampaignId, [itemId], "keep"));

        // Act
        var error = (await fixture.SendAsync(fixture.Source.ReviewerUserId,
            fixture.Source.Decision(launched.CampaignId, itemId, 1, "keep", "Reviewed"))).Error;
        var after = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new GetAccessReviewCampaign(fixture.Source.TenantId, launched.CampaignId));

        // Assert
        Assert.True(before.IsSuccess);
        Assert.True(preview.IsSuccess);
        Assert.NotNull(error);
        Assert.Equal(RequestErrorKind.Forbidden, error.Kind);
        Assert.True(after.IsSuccess);
        Assert.Equal(before.Value!.Revision, after.Value!.Revision);
        Assert.All(after.Value.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.All(after.Value.Items, item => Assert.Equal(1, item.Revision));
    }

    sealed class Fixture(AccessReviewFixture source, ServiceProvider provider) : IAsyncDisposable
    {
        public AccessReviewFixture Source { get; } = source;
        public ServiceProvider Provider { get; } = provider;

        public static async Task<Fixture> CreateAsync(string affiliation = "client_personnel")
        {
            var source = await AccessReviewFixture.CreateAsync();
            var events = source.Provider.GetRequiredService<IEventStore>();
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build();
            services.AddCompliance(configuration, developerAuthentication: true);
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton(source.Provider.GetRequiredService<IImmutableContentStore>());
            services.AddSingleton<IPermissionAuthorizer>(source.Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
            services.AddSingleton<IAccessReviewSources>(source.Sources);
            services.AddSingleton<IApplicationDirectoryReader>(source.Applications);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new MembershipDirectory(source.TenantId, affiliation));
            return new Fixture(source, services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }));
        }

        public OpenAccessPopulation Open() => new(Uuid.CreateVersion4(), Source.TenantId, Source.ApplicationId, Source.InstanceId, 1,
            AccessReviewFixture.Observed, "Synthetic observed export");

        public async Task<Result<T>> SendAsync<T>(Uuid userId, IRequest<T> request, bool mcp = false)
        {
            await using var scope = Provider.CreateAsyncScope();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(userId),
                mcp ? new McpInvocation("synthetic.access-review") : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, context, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Source.DisposeAsync();
        }
    }

    sealed class MembershipDirectory(Uuid tenantId, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenant == tenantId.ToString() ? new TenantMembershipView(userId, tenantId, affiliation) : null);
        public ValueTask<bool> IsMemberAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == tenantId.ToString());
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
