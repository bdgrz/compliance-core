using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
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

namespace Bdgrz.Compliance.Tests.Features.Programs;

[Trait("Category", "WebIntegration")]
public sealed class ProductClientServiceNativeHttpTests
{
    [Fact]
    public async Task ShouldReturnOriginalRegistrationAndUnitBytesGivenRetirementAndCurrentAuthorization()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var body = Value("Payroll");
        using var first = await fixture.Client.PutAsJsonAsync(fixture.CreatePath +
            $"?tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}&service_id={Uuid.CreateVersion4()}", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        var revision = fixture.Path + $"/revisions/{Uuid.CreateVersion4()}";
        using var revised = await fixture.Client.PutAsJsonAsync(revision, new { expected_revision = 1, name = "Second", purpose = "Monthly payroll", owner_reference = "Operations" });
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        var retirement = fixture.Path + $"/retirements/{Uuid.CreateVersion4()}";
        var retireBody = new { expected_revision = 2, rationale = "Service ended" };
        using var firstRetire = await fixture.Client.PutAsJsonAsync(retirement, retireBody);
        Assert.Equal(HttpStatusCode.OK, firstRetire.StatusCode);

        // Act
        using var createReplay = await fixture.Client.PutAsJsonAsync(fixture.CreatePath, body);
        using var retireReplay = await fixture.Client.PutAsJsonAsync(retirement, retireBody);
        fixture.Permissions.Allowed = false;
        using var denied = await fixture.Client.PutAsJsonAsync(retirement, retireBody);
        var history = await fixture.HistoryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, createReplay.StatusCode);
        Assert.Equal(original, await createReplay.Content.ReadAsStringAsync());
        using var registration = JsonDocument.Parse(original);
        Assert.Equal(fixture.Service.ToString(), registration.RootElement.GetProperty("service_id").GetString());
        Assert.Equal("{}", await revised.Content.ReadAsStringAsync());
        Assert.Equal("{}", await firstRetire.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retireReplay.StatusCode);
        Assert.Equal(await firstRetire.Content.ReadAsStringAsync(), await retireReplay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(3, history.Facts.Count);
        Assert.False(history.Owner.IsActive);
        Assert.All(history.Facts.OfType<IProductClientServiceFact>(), fact =>
        {
            Assert.Equal(fixture.Tenant, fact.TenantId);
            Assert.Equal(fixture.Program, fact.ProgramId);
            Assert.Equal(fixture.Service, fact.ServiceId);
        });
    }

    [Fact]
    public async Task ShouldRejectChangedIntentGivenSameServiceCreationIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        using var first = await fixture.Client.PutAsJsonAsync(fixture.CreatePath, Value("Payroll"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Act
        using var changed = await fixture.Client.PutAsJsonAsync(fixture.CreatePath, Value("Other"));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Single((await fixture.HistoryAsync()).Facts);
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("invalid-id")]
    [InlineData("invalid-body")]
    public async Task ShouldRejectMalformedRequiredIdentityOrBodyGivenServiceMutation(string invalid)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var path = invalid switch
        {
            "empty-id" => $"/api/v1/tenants/{fixture.Tenant}/programs/{fixture.Program}/client-services/{Uuid.Empty}",
            "invalid-id" => fixture.Path + "/revisions/not-a-uuid",
            _ => fixture.CreatePath,
        };
        using var body = new StringContent(invalid == "invalid-body" ? "{" :
            "{\"name\":\"Payroll\",\"purpose\":\"Process payroll\",\"owner_reference\":\"Operations\"}", Encoding.UTF8, "application/json");

        // Act
        using var response = await fixture.Client.PutAsync(path, body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await fixture.HistoryAsync()).Facts);
    }

    static object Value(string name) => new { name, purpose = "Process payroll", owner_reference = "Operations" };

    sealed class Fixture : IAsyncDisposable
    {
        readonly WebApplicationFactory<global::Program> _factory;
        readonly InMemoryEventStore _events = new();
        public HttpClient Client { get; }
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Service { get; } = Uuid.CreateVersion4();
        public MutablePermissions Permissions { get; } = new();
        public string Path => $"/api/v1/tenants/{Tenant}/client-services/{Service}";
        public string CreatePath => $"/api/v1/tenants/{Tenant}/programs/{Program}/client-services/{Service}";

        public Fixture()
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "product-client-service-native-http");
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
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            Client = _factory.CreateClient();
        }

        public async Task SeedAsync()
        {
            using var login = await Client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = "product-client-service@example.test" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await using var scope = _factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var rules = new ProgramRules(Tenant, Program);
            Assert.Null(rules.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                Uuid.CreateVersion4(), "Program author", DateTimeOffset.UtcNow));
            await ProgramSeeds.WriteAsync(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateWriter>(),
                services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), rules);
        }

        public async Task<ProductClientServiceHistorySnapshot> HistoryAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ProductClientServiceReader>().HistoryAsync(Tenant, Service);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
