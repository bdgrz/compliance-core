using Bdgrz.Compliance.Features.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
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

namespace Bdgrz.Compliance.Tests.Features.Risks;

[Trait("Category", "WebIntegration")]
public sealed class RiskDraftHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginal200BytesAndRequireCurrentGrantGivenRiskDraftWritesAfterLaterState()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var id = RiskDraft.IdFor(fixture.Tenant, fixture.Program, "R-HTTP");
        var creation = new CreateRiskDraft(id, fixture.Tenant, fixture.Program, "R-HTTP", "Original title", "Scenario", "Effect");
        using var first = await fixture.CreateAsync(creation);
        var firstBody = await first.Content.ReadAsStringAsync();
        var revision = new ReviseRiskDraft(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, id, 1, "First revision", "Scenario", "Effect");
        using var revised = await fixture.ReviseAsync(revision);
        using var later = await fixture.ReviseAsync(revision with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, Title = "Later state" });

        // Act
        using var creationReplay = await fixture.CreateAsync(creation);
        using var revisionReplay = await fixture.ReviseAsync(revision);
        using var conflict = await fixture.ReviseAsync(revision with { Title = "Different authored intent" });
        fixture.Permissions.Allowed = false;
        using var deniedReplay = await fixture.ReviseAsync(revision);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creationReplay.StatusCode);
        Assert.Equal(firstBody, await creationReplay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, revisionReplay.StatusCode);
        Assert.Equal("{}", await revised.Content.ReadAsStringAsync());
        Assert.Equal("{}", await revisionReplay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedReplay.StatusCode);
        Assert.Equal(3, await fixture.CountAsync(id));
    }

    [Fact]
    public async Task ShouldRefuseMissingTypedRevisionIdentityGivenProductionRiskRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var id = RiskDraft.IdFor(fixture.Tenant, fixture.Program, "R-MISSING-ID");
        using var created = await fixture.CreateAsync(new CreateRiskDraft(id, fixture.Tenant, fixture.Program, "R-MISSING-ID", "Title", "Scenario", "Effect"));

        // Act
        using var missing = await fixture.PutBodyAsync($"/{id}/draft", "{\"expected_revision\":1,\"title\":\"Changed\",\"scenario\":\"Scenario\",\"potential_effect\":\"Effect\"}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(1, await fixture.CountAsync(id));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public MutablePermissions Permissions { get; } = new();
        public Fixture()
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "risk-draft-native-http-tests");
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
                    services.AddSingleton<IPermissionAuthorizer>(Permissions);
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
                });
            });
            _client = _factory.CreateClient();
        }
        public async Task InitializeAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"risk-native-http@example.com\"}", Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await using var scope = _factory.Services.CreateAsyncScope();
            var program = new ProgramRules(Tenant, Program);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Program author", DateTimeOffset.UtcNow));
            await ProgramSeeds.WriteAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
                scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
                scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), program);
        }
        public Task<HttpResponseMessage> CreateAsync(CreateRiskDraft request) => PutBodyAsync($"/{request.RiskId}", JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.CreateRiskDraft));
        public Task<HttpResponseMessage> ReviseAsync(ReviseRiskDraft request) => PutBodyAsync($"/{request.RiskId}/draft", JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.ReviseRiskDraft));
        public async Task<HttpResponseMessage> PutBodyAsync(string suffix, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/risks{suffix}", body);
        }
        public async Task<int> CountAsync(Uuid risk)
        {
            var count = 0;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), RiskDraft.Area, risk.ToString()), EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    count++;
            return count;
        }
        public async ValueTask DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }
    }
    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
