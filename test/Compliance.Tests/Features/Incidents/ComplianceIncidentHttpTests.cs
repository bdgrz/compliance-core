using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Projections;
using Bdgrz.Compliance.Features.Incidents.Projectors;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Programs;
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

namespace Bdgrz.Compliance.Tests.Features.Incidents;

[Trait("Category", "WebIntegration")]
public sealed class ComplianceIncidentHttpTests
{
    [Fact]
    public async Task ShouldUseCanonicalIncidentRouteGivenMisleadingQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        var incidentId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{incidentId}?incident_id={Uuid.CreateVersion4()}&" +
            $"tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(incidentId.ToString(), body.RootElement.GetProperty("compliance_incident_id").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
    }

    [Fact]
    public async Task ShouldReturnActualNativeSummaryGivenProductionScopedListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var incidentId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(incidentId.ToString(), summary.GetProperty("compliance_incident_id").GetString());
        Assert.Equal("Governed production incident", summary.GetProperty("name").GetString());
        Assert.Equal("recorded", summary.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldHideIncidentGivenAnotherExistingProgramRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var incidentId = await fixture.SeedAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);

        // Act
        using var response = await fixture.GetAsync($"/{incidentId}", otherProgram);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenGetAndListRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var incidentId = await fixture.SeedAsync();

        // Act
        using var get = await fixture.GetAsync($"/{incidentId}");
        using var list = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task ShouldDescribeCanonicalRouteAndIdentifiedMutationsGivenIncidentHttpContract()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var document = JsonDocument.Parse(await fixture.OpenApiAsync());

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        var route = paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}");
        var get = route.GetProperty("get");
        Assert.True(route.TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}/revisions/{command_id}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}/assessments/{command_id}").TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{compliance_incident_id}", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/incidents/{incident_id}/resolutions/{command_id}").TryGetProperty("put", out _));
        var parameters = get.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(["incident_id", "program_id", "tenant_id"],
            parameters.Select(value => value.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, value =>
        {
            Assert.Equal("path", value.GetProperty("in").GetString());
            Assert.True(value.GetProperty("required").GetBoolean());
        });
    }

    [Fact]
    public async Task ShouldReturnOriginalUnit200AndEnforceCurrentAuthorizationGivenIdentifiedIncidentRetries()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = new RecordComplianceIncident(fixture.Tenant, fixture.Program, Uuid.CreateVersion4(), fixture.Body("HTTP native incident"));

        // Act
        using var first = await fixture.PutAsync(request);
        using var replay = await fixture.PutAsync(request);
        var revision = new ReviseComplianceIncident(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, request.IncidentId, 1,
            request.Content with { Title = "Corrected HTTP incident" }, "Correction");
        using var revised = await fixture.ReviseAsync(revision);
        fixture.Permissions.Allowed = false;
        using var denied = await fixture.PutAsync(request);
        fixture.Permissions.Allowed = true;
        using var original = await fixture.PutAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("{}", await first.Content.ReadAsStringAsync());
        Assert.Equal("{}", await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("{}", await original.Content.ReadAsStringAsync());
        Assert.Equal(2, await fixture.CountAsync(request.IncidentId));
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        public MutablePermissionAuthorizer Permissions { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();

        public Fixture(bool allowed = true)
        {
            Permissions.Allowed = allowed;
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "incident-read-http-tests");
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                        services.Remove(descriptor);
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IDomainEventReader>();
                    var receipts = new ReceiptTrackingEventStore(_events, TimeProvider.System);
                    services.AddSingleton<IEventStore>(receipts);
                    services.AddSingleton<IDomainEventReader>(receipts);
                    services.RemoveAll<INativeEventReceiptReader>();
                    services.AddSingleton<INativeEventReceiptReader>(receipts);
                    services.RemoveAll<IKvClient>();
                    services.AddSingleton<IKvClient>(new InMemoryKvClient());
                    services.RemoveAll<ITenantMembershipDirectoryReader>();
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                    services.RemoveAll<ITenantActivity>();
                    services.AddSingleton<ITenantActivity, ActiveTenant>();
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(Permissions);
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task<Uuid> SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"incident-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var package = new ComplianceIncidentRules(Tenant, Program, Uuid.CreateVersion4());
            Assert.Null(package.Record(Body("Governed production incident"), Member, "Incident recorder", Now));
            await IncidentSeeds.WriteAsync(writer, content, package);
            var projection = scope.ServiceProvider.GetRequiredService<IComplianceIncidentDirectoryProjection>();
            var identity = new CheckpointIdentity(ComplianceIncidentDirectoryProjector.ProjectorName,
                EventStreamPattern.ForPattern(Tenant.ToString(), ComplianceIncident.Area));
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            var cursor = EventCursor.Start;
            await foreach (var record in _events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is IComplianceIncidentFact)
                    await projection.ApplyAsync(record.Event);
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

        public ComplianceIncidentContent Body(string title) => new(null, null, null, null, title, "Complete governed incident chronology.", "security", "high",
            Now.AddHours(-2), Now.AddHours(-1), Member, true, false, [Uuid.CreateVersion4()], "confidential", []);
        public async Task<HttpResponseMessage> ReviseAsync(ReviseComplianceIncident request)
        {
            using var body = new StringContent(JsonSerializer.Serialize(request, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ReviseComplianceIncident), Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/incidents/{request.IncidentId}/revisions/{request.CommandId}", body);
        }
        public async Task<HttpResponseMessage> PutAsync(RecordComplianceIncident request)
        {
            using var body = new StringContent(JsonSerializer.Serialize(request, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.RecordComplianceIncident), Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/incidents/{request.IncidentId}", body);
        }
        public async Task<int> CountAsync(Uuid incidentId)
        {
            var count = 0;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), ComplianceIncident.Area, incidentId.ToString()), EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    count++;
            return count;
        }

        public Task<string> OpenApiAsync() => _client.GetStringAsync("/openapi/v1.json");

        public Task<HttpResponseMessage> GetAsync(string suffix, Uuid? program = null) =>
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/incidents{suffix}");

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class MutablePermissionAuthorizer : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenant, Uuid user, Uuid member, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
