using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.NamedRecords;

/// <summary>
///     The six identified PUT admissions of the named records over HTTP and MCP: every exact retry answers the same
///     frozen 202, a malformed identity is refused before any append, and the old create and revise routes are gone.
/// </summary>
[Trait("Category", "WebIntegration")]
public sealed class NamedRecordTransportTests
{
    [Theory]
    [InlineData("provider", false)]
    [InlineData("provider", true)]
    [InlineData("location", false)]
    [InlineData("location", true)]
    [InlineData("process", false)]
    [InlineData("process", true)]
    public async Task ShouldAcceptRecordAndRevisionWithFrozenResponseGivenExactRetries(string kind, bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var owner = await SeedOwnerAsync(factory, tenant);
        var (recordId, revisedId, command) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await SeedAsync(factory, kind, tenant, revisedId, owner);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        var record = await PutAsync(client, scenario, kind, tenant, recordId, null, owner);
        var recordRetry = await PutAsync(client, scenario, kind, tenant, recordId, null, owner);
        var revise = await PutAsync(client, scenario, kind, tenant, revisedId, command, owner);
        var reviseRetry = await PutAsync(client, scenario, kind, tenant, revisedId, command, owner);

        // Assert
        Assert.Equal(record, recordRetry);
        Assert.Equal(revise, reviseRetry);
        Assert.Equal(recordId.ToString(), JsonDocument.Parse(record).RootElement.GetProperty("operation_id").GetString());
        Assert.Equal(command.ToString(), JsonDocument.Parse(revise).RootElement.GetProperty("operation_id").GetString());
        var accepted = await AcceptedCountAsync(factory, kind, tenant, recordId);
        Assert.Equal(1, accepted);
    }

    [Theory]
    [InlineData("providers", "/revise-provider/")]
    [InlineData("locations", "/revise-location/")]
    [InlineData("operational-processes", "/revise-operational-process/")]
    public async Task ShouldRefuseMalformedIdentityAndRetireOldRoutesGivenNamedRecordRoutes(string area, string reviseSegment)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();

        // Act
        using var malformedRecord = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant}/{area}/not-a-uuid", new { });
        using var emptyRecord = await client.PutAsJsonAsync(
            $"/api/v1/tenants/{tenant}/{area}/00000000-0000-0000-0000-000000000000", new { });
        using var malformedRevision = await client.PutAsJsonAsync(
            $"/api/v1/tenants/{tenant}/{area}{reviseSegment}not-a-uuid", new { });
        using var oldCreate = await client.PostAsJsonAsync($"/api/v1/tenants/{tenant}/{area}", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, malformedRecord.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyRecord.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedRevision.StatusCode);
        Assert.True(oldCreate.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("bdgrz.provider.record", "provider_id")]
    [InlineData("bdgrz.provider.revise", "command_id")]
    [InlineData("bdgrz.inventory.location.record", "location_id")]
    [InlineData("bdgrz.inventory.location.revise", "command_id")]
    [InlineData("bdgrz.inventory.operational_process.record", "operational_process_id")]
    [InlineData("bdgrz.inventory.operational_process.revise", "command_id")]
    public async Task ShouldRequireTypedIdentityGivenMcpNamedRecordTool(string tool, string identity)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        // Act
        var tools = await scenario.ListTools();

        // Assert
        var listed = Assert.Single(tools, item => item.Name == tool);
        var required = listed.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(identity, required);
    }

    static async Task<string> PutAsync(HttpClient client, McpScenario? scenario, string kind, Uuid tenant, Uuid id,
        Uuid? command, Uuid owner)
    {
        var revise = command is not null;
        if (scenario is not null)
        {
            var tool = kind switch
            {
                "provider" => revise ? "bdgrz.provider.revise" : "bdgrz.provider.record",
                "location" => revise ? "bdgrz.inventory.location.revise" : "bdgrz.inventory.location.record",
                _ => revise ? "bdgrz.inventory.operational_process.revise" : "bdgrz.inventory.operational_process.record",
            };
            var arguments = Arguments(kind, tenant, id, command, owner);
            var snapshot = await scenario.When(tool, arguments).ExpectSuccess();
            return Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result").GetRawText();
        }

        var path = Path(kind, tenant, id, command);
        using var body = new StringContent(JsonSerializer.Serialize(Arguments(kind, tenant, id, command, owner)
            .Where(item => item.Key is not ("tenant_id" or "command_id") &&
                (revise || item.Key is not ("provider_id" or "location_id" or "operational_process_id")))
            .ToDictionary(static item => item.Key, static item => item.Value)), Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, text);
        return text;
    }

    static string Path(string kind, Uuid tenant, Uuid id, Uuid? command)
    {
        var area = kind switch { "provider" => "providers", "location" => "locations", _ => "operational-processes" };
        var revise = kind switch { "provider" => "revise-provider", "location" => "revise-location", _ => "revise-operational-process" };
        return command is null
            ? $"/api/v1/tenants/{tenant}/{area}/{id}"
            : $"/api/v1/tenants/{tenant}/{area}/{revise}/{command}";
    }

    static Dictionary<string, object?> Arguments(string kind, Uuid tenant, Uuid id, Uuid? command, Uuid owner)
    {
        var revise = command is not null;
        var arguments = new Dictionary<string, object?> { ["tenant_id"] = tenant.ToString() };
        if (revise)
        {
            arguments["command_id"] = command!.Value.ToString();
            arguments["expected_revision"] = 1;
        }

        switch (kind)
        {
            case "provider":
                arguments["provider_id"] = id.ToString();
                arguments["content"] = JsonSerializer.SerializeToElement(
                    new ProviderContent(revise ? "Changed supplier" : "Supplier", "supplier"),
                    ComplianceCoreJsonContext.Default.ProviderContent);
                break;
            case "location":
                arguments["location_id"] = id.ToString();
                arguments["name"] = revise ? "Changed site" : "Site";
                arguments["owner_person_id"] = owner.ToString();
                if (revise)
                    arguments["lifecycle"] = "active";
                else
                    arguments["kind"] = "physical_site";
                break;
            default:
                arguments["operational_process_id"] = id.ToString();
                arguments["name"] = revise ? "Changed process" : "Process";
                arguments["purpose"] = "Purpose";
                arguments["owner_person_id"] = owner.ToString();
                if (revise)
                    arguments["lifecycle"] = "active";
                break;
        }

        return arguments;
    }

    static async Task SeedAsync(WebApplicationFactory<Program> factory, string kind, Uuid tenant, Uuid id, Uuid owner)
    {
        switch (kind)
        {
            case "provider":
                await NamedRecordFacts.SeedProviderAsync(factory.Services, tenant, id,
                    new ProviderContent("Seeded supplier", "supplier"));
                break;
            case "location":
                await NamedRecordFacts.SeedLocationAsync(factory.Services, tenant, id,
                    new LocationContent("physical_site", "Seeded site", null, owner, "active"));
                break;
            default:
                await NamedRecordFacts.SeedProcessAsync(factory.Services, tenant, id,
                    new OperationalProcessContent("Seeded process", "Purpose", owner, null, null, "active"));
                break;
        }
    }

    static async Task<Uuid> SeedOwnerAsync(WebApplicationFactory<Program> factory, Uuid tenant)
    {
        var owner = Uuid.CreateVersion4();
        await using var scope = factory.Services.CreateAsyncScope();
        var rules = new PersonRules(tenant, owner);
        var result = rules.Record("Owner", null, ActorReference.ForMember(Uuid.CreateVersion4(), "Seed"), DateTimeOffset.UtcNow);
        await PersonSeeds.AppendAsync(scope.ServiceProvider, rules);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return owner;
    }

    static async Task<int> AcceptedCountAsync(WebApplicationFactory<Program> factory, string kind, Uuid tenant, Uuid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var count = kind switch
        {
            "provider" => new NamedRecordsCount((await reader.HydrateAsync(new Provider(tenant, id))).Generation),
            "location" => new NamedRecordsCount((await reader.HydrateAsync(new Location(tenant, id))).Generation),
            _ => new NamedRecordsCount((await reader.HydrateAsync(new OperationalProcess(tenant, id))).Generation),
        };
        return (int)count.Generation;
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"named-record-transport@example.com\"}", Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    sealed record NamedRecordsCount(long Generation);
}
