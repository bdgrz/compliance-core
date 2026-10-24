using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Features.AccessControl;
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

namespace Bdgrz.Compliance.Tests.Features.Providers;

[Trait("Category", "WebIntegration")]
public sealed class ProviderHttpMcpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAcceptRecordAndRevisionWithFrozenResponseGivenAuthorizedHttpOrMcpRetries(bool mcp)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new Login("provider-author@example.com"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        var recordedId = Uuid.CreateVersion4();
        var revisedId = Uuid.CreateVersion4();
        var revisionCommand = Uuid.CreateVersion4();
        var content = new ProviderContent("Supplier", "Authored kind", "not_material", [], "No customer data or critical path");
        await NamedRecordFacts.SeedProviderAsync(factory.Services, tenantId, revisedId, content with { Name = "Other supplier" });
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        var record = await RecordAsync();
        var recordRetry = await RecordAsync();
        var revise = await ReviseAsync();
        var reviseRetry = await ReviseAsync();
        var recorded = await ProgramManagementServices.HydrateAsync(factory.Services, new Provider(tenantId, recordedId));
        var revising = await ProgramManagementServices.HydrateAsync(factory.Services, new Provider(tenantId, revisedId));

        // Assert
        Assert.Equal(record, recordRetry);
        Assert.Equal(revise, reviseRetry);
        Assert.Equal(recordedId.ToString(), JsonDocument.Parse(record).RootElement.GetProperty("operation_id").GetString());
        Assert.Equal(revisionCommand.ToString(), JsonDocument.Parse(revise).RootElement.GetProperty("operation_id").GetString());
        Assert.Equal(1L, recorded.Generation);
        Assert.Equal(0L, recorded.Revision);
        Assert.Equal(2L, revising.Generation);
        Assert.Equal(1L, revising.Revision);
        Assert.Equal(revisionCommand, revising.Pending!.RootOperationId);

        async Task<string> RecordAsync()
        {
            if (scenario is not null)
                return Result(await scenario.When("bdgrz.provider.record", new Dictionary<string, object?>
                {
                    ["provider_id"] = recordedId.ToString(),
                    ["tenant_id"] = tenantId.ToString(),
                    ["content"] = JsonSerializer.SerializeToElement(content, ComplianceCoreJsonContext.Default.ProviderContent),
                }).ExpectSuccess()).GetRawText();
            using var body = Body(content);
            using var response = await client.PutAsync($"/api/v1/tenants/{tenantId}/providers/{recordedId}", body);
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Accepted, text);
            return text;
        }

        async Task<string> ReviseAsync()
        {
            var changed = content with { Name = "Changed supplier" };
            if (scenario is not null)
                return Result(await scenario.When("bdgrz.provider.revise", new Dictionary<string, object?>
                {
                    ["command_id"] = revisionCommand.ToString(),
                    ["tenant_id"] = tenantId.ToString(),
                    ["provider_id"] = revisedId.ToString(),
                    ["expected_revision"] = 1,
                    ["content"] = JsonSerializer.SerializeToElement(changed, ComplianceCoreJsonContext.Default.ProviderContent),
                }).ExpectSuccess()).GetRawText();
            using var body = new StringContent("{\"provider_id\":\"" + revisedId + "\",\"expected_revision\":1,\"content\":" + JsonSerializer.Serialize(changed,
                ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");
            using var response = await client.PutAsync(
                $"/api/v1/tenants/{tenantId}/providers/revise-provider/{revisionCommand}", body);
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Accepted, text);
            return text;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReadEmptyTenantRegisterGivenAuthorizedHttpOrMcpCaller(bool mcp)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new Login("provider-reader@example.com"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        JsonElement page;
        if (scenario is not null)
        {
            var listed = await scenario.When("bdgrz.providers.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["limit"] = 1,
            }).ExpectSuccess();
            page = Result(listed);
        }
        else
        {
            using var listed = await client.GetAsync($"/api/v1/tenants/{tenantId}/providers?limit=1");
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            using var json = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
            page = json.RootElement.Clone();
        }

        // Assert
        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
    }

    static JsonElement Result(McpCallSnapshot snapshot) => Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result");

    static StringContent Body(ProviderContent content) => new("{\"content\":" + JsonSerializer.Serialize(content,
        ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");

    internal static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
        builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
        builder.UseSetting("Fitz:ApplicationName", "provider-register-tests");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                         descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                services.Remove(descriptor);
            var store = new InMemoryEventStore();
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.RemoveAll<IKvClient>();
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.RemoveAll<ISourceFactResolver>();
            services.AddScoped<ISourceFactResolver>(provider =>
                new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
            services.RemoveAll<ITenantMembershipDirectoryReader>();
            services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
            services.RemoveAll<ITenantActivity>();
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.RemoveAll<IAccessGrantPermissionAuthorizer>();
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
        });
    });

    sealed record Login([property: JsonPropertyName("email_address")] string EmailAddress);
}
