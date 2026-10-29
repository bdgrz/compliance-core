using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
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
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Projections;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

[Trait("Category", "WebIntegration")]
public sealed class AuditPackageReadHttpTests
{
    [Fact]
    public async Task ShouldUseCanonicalPackageRouteGivenMisleadingQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{packageId}?audit_package_id={Uuid.CreateVersion4()}&" +
            $"tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(packageId.ToString(), body.RootElement.GetProperty("audit_package_id").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
    }

    [Fact]
    public async Task ShouldReturnActualNativeSummaryGivenProductionScopedListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(packageId.ToString(), summary.GetProperty("audit_package_id").GetString());
        Assert.Equal("Audit package", summary.GetProperty("name").GetString());
        Assert.Equal("draft", summary.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldHidePackageGivenAnotherExistingProgramRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);

        // Act
        using var response = await fixture.GetAsync($"/{packageId}", otherProgram);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenGetAndListRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var packageId = await fixture.SeedAsync();

        // Act
        using var get = await fixture.GetAsync($"/{packageId}");
        using var list = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task ShouldDescribeOnlyCanonicalRouteIdentityGivenCustomPackageReadBinder()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var document = JsonDocument.Parse(await fixture.OpenApiAsync());

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        var route = paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{package_id}");
        var get = route.GetProperty("get");
        Assert.True(route.TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/audit-packages/{audit_package_id}", out _));
        var parameters = get.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(["package_id", "program_id", "tenant_id"],
            parameters.Select(value => value.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, value =>
        {
            Assert.Equal("path", value.GetProperty("in").GetString());
            Assert.True(value.GetProperty("required").GetBoolean());
        });
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();

        public Fixture(bool allowed = true)
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "audit-package-read-http-tests");
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                        services.Remove(descriptor);
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IDomainEventReader>();
                    services.AddSingleton<IEventStore>(_events);
                    services.AddSingleton<IDomainEventReader>(_events);
                    services.RemoveAll<IKvClient>();
                    services.AddSingleton<IKvClient>(new InMemoryKvClient());
                    services.RemoveAll<ITenantMembershipDirectoryReader>();
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                    services.RemoveAll<ITenantActivity>();
                    services.AddSingleton<ITenantActivity, ActiveTenant>();
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task<Uuid> SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"package-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var package = new AuditPackageRules(Tenant, Program, Uuid.CreateVersion4());
            Assert.Null(package.Create(new AuditPackageContent(Uuid.CreateVersion4(), "type_i", "type_i_baseline",
                Uuid.CreateVersion4(), new string('a', 64), "Audit package",
                [new AuditPackageSelectionEntry("system_description", "source", "system_description",
                    Uuid.CreateVersion4(), 1, "snapshot", new string('b', 64), 100, "management",
                    Uuid.CreateVersion4())]), Member, "Package author", Now));
            await PackageSeeds.WriteAsync(reader, writer, _events, content, package.Current!);
            var projection = scope.ServiceProvider.GetRequiredService<IAuditPackageDirectoryProjection>();
            var identity = new CheckpointIdentity(FitzAuditPackageDirectory.ProjectorName,
                EventStreamPattern.ForPattern(Tenant.ToString(), AuditPackageManifest.Area));
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            var cursor = EventCursor.Start;
            await foreach (var record in _events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is AuditPackageCreated created)
                    await projection.ApplyAsync(created);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            return package.Id;
        }

        public async Task SeedProgramAsync(Uuid programId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var program = new ProgramRules(Tenant, programId);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                Member, "Program lead", Now));
            await ProgramSeeds.WriteAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
                scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
                scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), program);
        }

        public Task<string> OpenApiAsync() => _client.GetStringAsync("/openapi/v1.json");

        public Task<HttpResponseMessage> GetAsync(string suffix, Uuid? program = null) =>
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/audit-packages{suffix}");

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
