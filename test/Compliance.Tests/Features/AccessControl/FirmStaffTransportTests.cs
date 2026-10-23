using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class FirmStaffTransportTests
{
    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenRegisterRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();

        // Act
        using var first = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/register-firm-staff",
            Body(user, "attest"));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/register-firm-staff",
            Body(user, "attest"));
        var retryText = await retried.Content.ReadAsStringAsync();
        using var changed = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/register-firm-staff",
            Body(user, "advisory"));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(staff.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/platform/operations/{staff}", document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal($"/api/v1/platform/operations/{staff}", first.Headers.Location?.OriginalString);
        Assert.True(document.RootElement.TryGetProperty("accepted_at", out _));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    public async Task ShouldRejectGivenMissingOrMalformedRouteIdentity(string staff, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);

        // Act
        using var response = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/register-firm-staff",
            Body(Uuid.CreateVersion4(), "attest"));

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnOriginalViewGivenStatusRouteRetriedAfterLaterChange()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var staff = Uuid.CreateVersion4();
        using var accepted = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/register-firm-staff",
            Body(Uuid.CreateVersion4(), "attest"));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        await FirmStaffFacts.RunProcessAsync(factory.Services, staff);
        var command = Uuid.CreateVersion4();
        var path = $"/api/v1/platform/firm-staff/{staff}/set-firm-staff-status/{command}";

        // Act
        using var first = await client.PutAsync(path, Status(false, "Synthetic deactivation", 1));
        using var later = await client.PutAsync(
            $"/api/v1/platform/firm-staff/{staff}/set-firm-staff-status/{Uuid.CreateVersion4()}",
            Status(true, "Synthetic reactivation", 2));
        using var retried = await client.PutAsync(path, Status(false, "Synthetic deactivation", 1));
        using var changed = await client.PutAsync(path, Status(false, "Another reason", 1));
        using var oldRoute = await client.PutAsync($"/api/v1/platform/firm-staff/{staff}/status/{command}",
            Status(false, "Synthetic deactivation", 1));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await retried.Content.ReadAsStringAsync());
        using var view = JsonDocument.Parse(await retried.Content.ReadAsStringAsync());
        Assert.False(view.RootElement.GetProperty("is_active").GetBoolean());
        Assert.Equal(2, view.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
    }

    [Fact]
    public async Task ShouldExposeRequiredIdentitiesGivenMcpTools()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var staff = Uuid.CreateVersion4();

        // Act
        var tools = await scenario.ListTools();
        var registered = await scenario.When("bdgrz.firm-staff.register", new Dictionary<string, object?>
        {
            ["staff_member_id"] = staff.ToString(),
            ["user_id"] = Uuid.CreateVersion4().ToString(),
            ["practice"] = "attest",
            ["source_reference"] = "Synthetic staff source",
            ["expected_revision"] = 0,
        }).ExpectSuccess();
        var missingCommand = await scenario.When("bdgrz.firm-staff.status.set", new Dictionary<string, object?>
        {
            ["staff_member_id"] = staff.ToString(),
            ["is_active"] = false,
            ["reason"] = "Synthetic",
            ["expected_revision"] = 1,
        });

        // Assert
        var register = Assert.Single(tools, item => item.Name == "bdgrz.firm-staff.register");
        Assert.Contains("staff_member_id", register.InputSchema.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()));
        var status = Assert.Single(tools, item => item.Name == "bdgrz.firm-staff.status.set");
        Assert.Contains("command_id", status.InputSchema.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()));
        Assert.Equal(staff.ToString(), Assert.IsType<JsonElement>(registered.StructuredJson).GetProperty("result")
            .GetProperty("operation_id").GetString());
        Assert.True(missingCommand.IsError);
    }

    static StringContent Body(Uuid user, string practice) => new("{\"user_id\":\"" + user + "\",\"practice\":\"" +
        practice + "\",\"source_reference\":\"Synthetic staff source\",\"expected_revision\":0}", Encoding.UTF8,
        "application/json");

    static StringContent Status(bool active, string reason, int revision) => new("{\"is_active\":" +
        (active ? "true" : "false") + ",\"reason\":\"" + reason + "\",\"expected_revision\":" +
        revision.ToString(CultureInfo.InvariantCulture) + "}", Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlatformUserDirectoryReader>();
            services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
        }));

    static async Task LoginAndSeedAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var actor = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "firm-staff-transport@example.com"),
            CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, [actor]), RequestActor.System)).IsSuccess);
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
