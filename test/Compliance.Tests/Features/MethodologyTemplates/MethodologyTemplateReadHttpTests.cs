using Bdgrz.Compliance.Features.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
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

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

[Trait("Category", "WebIntegration")]
public sealed class MethodologyTemplateReadHttpTests
{
    [Fact]
    public async Task ShouldRequireAuthenticationGivenScopedTemplateApplicationRead()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var response = await fixture.GetTemplateApplicationAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnEmptyPublishedCatalogGivenAuthorizedProductionListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        // Act
        using var response = await fixture.ListAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenBothPublishedMethodologyRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        await fixture.SeedAsync();

        // Act
        using var list = await fixture.ListAsync();
        using var version = await fixture.GetAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, version.StatusCode);
    }

    [Fact]
    public async Task ShouldHideCatalogGivenAnotherProgramOutsideTenantScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        // Act
        using var response = await fixture.ListAsync(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
                builder.UseSetting("Fitz:ApplicationName", "methodology-read-http-tests");
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

        public async Task SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"methodology-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
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

        public Task<HttpResponseMessage> ListAsync(Uuid? program = null) =>
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/methodology-templates");
        public Task<HttpResponseMessage> GetAsync() => _client.GetAsync(
            $"/api/v1/tenants/{Tenant}/programs/{Program}/methodology-template-versions/{Uuid.CreateVersion4()}");
        public Task<HttpResponseMessage> GetTemplateApplicationAsync() => _client.GetAsync(
            $"/api/v1/tenants/{Tenant}/programs/{Program}/template-applications/{Uuid.CreateVersion4()}");

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
