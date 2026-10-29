using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Boundaries.Projections;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class AttestProviderManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldDenyProviderAuthoringGivenActualAttestHistoryAndOrdinaryOrganizationGrant(bool revoked, bool mcp)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: revoked);
        await using var scope = provider.CreateAsyncScope();
        var providerId = Uuid.CreateVersion4();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordProvider(providerId, Tenant, new ProviderContent("Supplier", "Supplier")),
            Context(mcp), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Provider(Tenant, providerId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveProviderReadsAndPreviewButDenyRevisionGivenHistoricalAttestAssignment()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var providerId = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedProviderAsync(provider, Tenant, providerId, new ProviderContent("Supplier", "Supplier"));
        await ProjectProvidersAsync(scope.ServiceProvider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        await ProjectSetupAsync(scope.ServiceProvider);

        // Act
        var revision = await bus.DispatchAsync(new ReviseProvider(Uuid.CreateVersion4(), Tenant, providerId, 1,
            new ProviderContent("Changed", "Supplier")), Context(), CancellationToken.None);
        var read = await bus.DispatchAsync(new GetProvider(Tenant, providerId), Context(), CancellationToken.None);
        var preview = await bus.DispatchAsync(new PreviewProviderChange(Tenant, providerId, 1,
            "renewal", new DateOnly(2027, 1, 1), "Review renewal"), Context(), CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<ProviderSource>()
            .GetAsync(Tenant, providerId, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, revision.Error?.Kind);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(1, retained!.Revision);
        Assert.Equal("Supplier", retained.Content.Name);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPermitProviderAuthoringGivenNoSameClientActualAttestHistory(string practice, bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(), User,
            practice: practice);
        await using var scope = provider.CreateAsyncScope();
        var providerId = Uuid.CreateVersion4();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordProvider(providerId, Tenant, new ProviderContent("Supplier", "Supplier")), Context(),
            CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new Provider(Tenant, providerId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(providerId.ToString(), retained.Id.ToString());
        Assert.Equal(1L, retained.Generation);
        Assert.NotNull(retained.Pending);
    }

    static RequestDispatchContext Context(bool mcp = false) => new(ProgramManagementServices.Actor(User),
        mcp ? new McpInvocation("synthetic.provider") : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));

    [Fact]
    public async Task ShouldDenyProviderAuthoringGivenNoOrdinaryOrganizationGrantAndNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose(allowed: false);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordProvider(Uuid.CreateVersion4(), Tenant, new ProviderContent("Supplier", "Supplier")), Context(),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    static async Task ProjectProvidersAsync(IServiceProvider services)
    {
        var events = services.GetRequiredService<IDomainEventReader>();
        var projection = services.GetRequiredService<IProviderProjection>();
        var identity = new CheckpointIdentity("ProviderRegisterV1", EventStreamPattern.ForPattern(Tenant.ToString(), Provider.Area));
        var content = services.GetRequiredService<IImmutableContentStore>();
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var cursor = ProjectionCheckpoint.Start.Cursor;
        await foreach (var record in events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
        {
            if (record.Event is INamedRecordAppliedFact applied)
            {
                var bytes = await content.ReadVerifiedAsync(Tenant.ToString(), applied.ContentContent, CancellationToken.None);
                await projection.ApplyAsync(record.Event, System.Text.Json.JsonSerializer.Deserialize(bytes.Span,
                    ComplianceCoreJsonContext.Default.ProviderContent)!, CancellationToken.None);
            }
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static async Task ProjectSetupAsync(IServiceProvider services)
    {
        var events = services.GetRequiredService<IDomainEventReader>();
        var programs = services.GetRequiredService<IProgramDirectoryProjection>();
        var boundaries = services.GetRequiredService<IBoundaryDirectoryProjection>();
        var pattern = EventStreamPattern.ForPattern(Tenant.ToString());
        await using var programBatch = await programs.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ProgramDirectory", pattern), ProjectionCheckpoint.Start));
        await using var boundaryBatch = await boundaries.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName, pattern), ProjectionCheckpoint.Start));
        var cursor = ProjectionCheckpoint.Start.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await programs.ApplyAsync(record.Event, CancellationToken.None);
            await boundaries.ApplyAsync(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await programBatch.CommitAsync(new ProjectionCheckpoint(cursor));
        await boundaryBatch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static ServiceProvider Compose(bool allowed = true)
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
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(allowed)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(User, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
