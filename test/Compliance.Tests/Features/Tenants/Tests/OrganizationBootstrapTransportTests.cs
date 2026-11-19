using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>Real generated HTTP/MCP adapters and production authorizers, with workers driven separately by acceptance tests.</summary>
[Trait("Category", "WebIntegration")]
public sealed class OrganizationBootstrapTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayCreationAndRefuseChangedIntentGivenHttpOrMcpStableTenantIdentity(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var tenant = Uuid.CreateVersion4();
        var slug = $"transport-{tenant.ToString()[..8]}";

        // Act
        var first = await CreateAsync("Acme");
        var retry = await CreateAsync("Acme");
        if (scenario is not null)
        {
            var changed = await scenario.When("bdgrz.tenant.register", Arguments("Different organization"));
            Assert.True(changed.IsError);
        }
        else
        {
            using var body = Body("Different organization");
            using var changed = await client.PostAsync("/api/v1/tenants", body);
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Tenant(tenant));

        // Assert
        Assert.Equal(first, retry);
        using var firstJson = JsonDocument.Parse(first);
        Assert.Equal(tenant.ToString(), firstJson.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(1UL, owner.Version);

        Dictionary<string, object?> Arguments(string name) => new()
        {
            ["tenant_id"] = tenant.ToString(),
            ["name"] = name,
            ["slug"] = slug,
            ["legal_name"] = "Acme LLC",
        };

        StringContent Body(string name) => new(JsonSerializer.Serialize(Arguments(name)), Encoding.UTF8, "application/json");

        async Task<string> CreateAsync(string name)
        {
            if (scenario is not null)
            {
                var call = await scenario.When("bdgrz.tenant.register", Arguments(name)).ExpectSuccess();
                return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result").GetRawText();
            }
            using var body = Body(name);
            using var response = await client.PostAsync("/api/v1/tenants", body);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
            return responseBody;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ShouldRequireStableCreationIdentityGivenHttpOrMcpInput(string? tenantId)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var arguments = new Dictionary<string, object?>
        {
            ["name"] = "Acme",
            ["slug"] = "invalid-identity",
            ["legal_name"] = "Acme LLC",
        };
        if (tenantId is not null)
            arguments["tenant_id"] = tenantId;

        // Act
        var tools = await scenario.ListTools();
        var result = await scenario.When("bdgrz.tenant.register", arguments);
        using var body = new StringContent(JsonSerializer.Serialize(arguments), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/tenants", body);

        // Assert
        var tool = Assert.Single(tools, item => item.Name == "bdgrz.tenant.register");
        Assert.Contains("tenant_id", tool.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.IsError);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldExposeOnlyPublicPrimaryCommandsGivenMcpDiscovery()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        // Act
        var tools = await scenario.ListTools();

        // Assert
        var designate = Assert.Single(tools, item => item.Name == "bdgrz.rbac.designate-primary-org-admin");
        var release = Assert.Single(tools, item => item.Name == "bdgrz.rbac.release-primary-org-admin");
        _ = Assert.Single(tools, item => item.Name == "bdgrz.rbac.primary-org-admins.get");
        Assert.Contains("command_id", designate.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("membership_episode_id", designate.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("designation_id", release.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain(tools, item => item.Name == "bdgrz.rbac.mark-member-primary");
        Assert.DoesNotContain(tools, item => item.Name == "bdgrz.rbac.unmark-member-primary");
        Assert.DoesNotContain(tools, item => item.Name == "bdgrz.rbac.record-primary-mark-outcome");
    }

    [Fact]
    public async Task ShouldDenyCreationAndPrimaryManagementGivenAnonymousHttpActor()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        using var creationBody = new StringContent($"{{\"tenant_id\":\"{tenant}\",\"name\":\"Acme\",\"slug\":\"anonymous\",\"legal_name\":\"Acme LLC\"}}", Encoding.UTF8, "application/json");
        using var designationBody = new StringContent($"{{\"membership_episode_id\":\"{Uuid.CreateVersion4()}\"}}", Encoding.UTF8, "application/json");

        // Act
        using var created = await client.PostAsync("/api/v1/tenants", creationBody);
        using var designation = await client.PutAsync($"/api/v1/tenants/{tenant}/primary_org_admins/{user}/designations/{Uuid.CreateVersion4()}", designationBody);
        using var read = await client.GetAsync($"/api/v1/tenants/{tenant}/primary_org_admins");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, created.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, designation.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
        builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:1/ws");
        builder.UseSetting("Fitz:ApplicationName", "organization-bootstrap-transport");
        builder.UseSetting("Cassie:ConnectionString", "Host=127.0.0.1;Port=1;Database=postgres;Username=root;Password=test;SSL Mode=Disable");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name is not ("ComplianceReadinessLifecycle" or "NativeFirstResponseEndpointValidation")).ToArray())
                services.Remove(descriptor);
            var store = new InMemoryEventStore();
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.RemoveAll<IKvClient>();
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
        });
    });

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"organization-bootstrap-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
