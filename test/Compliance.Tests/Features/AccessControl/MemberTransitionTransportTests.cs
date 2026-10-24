using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class MemberTransitionTransportTests
{
    const string OperationId = "0a5b3f0b-6d8e-4d6b-a0c7-7a7bdbf20001";

    [Theory]
    [InlineData("suspend")]
    [InlineData("deprovision")]
    [InlineData("reinstate")]
    public async Task ShouldAcceptWithFrozenResponseAndAdmitOnceGivenExactHttpRetries(string route)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var episode = await RegisterAsync(factory, tenant, user, suspended: route == "reinstate");
        var path = route switch
        {
            "suspend" => $"/api/v1/tenants/{tenant}/members/{user}/suspensions/{OperationId}",
            "deprovision" => $"/api/v1/tenants/{tenant}/members/{user}/deprovisioning/{OperationId}",
            _ => $"/api/v1/tenants/{tenant}/members/by-user/{user}/reinstatements/{OperationId}",
        };
        var revision = route == "reinstate" ? 2 : 1;
        object body = route == "reinstate"
            ? new { member_episode_id = episode, expected_member_revision = revision }
            : new { reason = "Access is no longer required.", member_episode_id = episode, expected_member_revision = revision };

        // Act
        using var first = await client.PutAsJsonAsync(path, body);
        var firstBody = await first.Content.ReadAsStringAsync();
        using var retry = await client.PutAsJsonAsync(path, body);
        var retryBody = await retry.Content.ReadAsStringAsync();
        var facts = await GuardFactsAsync(factory, tenant);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(firstBody, retryBody);
        using var document = JsonDocument.Parse(firstBody);
        Assert.Equal(OperationId, document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal("accepted", document.RootElement.GetProperty("state").GetString());
        Assert.Single(facts.OfType<TenantManagerSourceMutationAdmitted>());
    }

    [Theory]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    public async Task ShouldRejectInvalidCommandIdentityGivenAnAuthorizedSuspension(string commandId,
        HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var episode = await RegisterAsync(factory, tenant, user, suspended: false);

        // Act
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/tenants/{tenant}/members/{user}/suspensions/{commandId}",
            new { reason = "Access is no longer required.", member_episode_id = episode, expected_member_revision = 1 });

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await GuardFactsAsync(factory, tenant));
    }

    [Fact]
    public async Task ShouldRetireTheOldMemberRoutesGivenIdentifiedMemberTransitions()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();

        // Act
        using var suspend = await client.PostAsJsonAsync($"/api/v1/tenants/{tenant}/members/{user}/suspensions", new { });
        using var reinstate = await client.DeleteAsync($"/api/v1/tenants/{tenant}/members/{user}/suspensions");
        using var deprovision = await client.PostAsJsonAsync($"/api/v1/tenants/{tenant}/members/{user}/deprovisioning", new { });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, suspend.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reinstate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deprovision.StatusCode);
    }

    [Theory]
    [InlineData("bdgrz.tenant.member.suspend")]
    [InlineData("bdgrz.tenant.member.reinstate")]
    public async Task ShouldRequireTypedIdentityGivenMcpMemberTransitionTool(string tool)
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
        Assert.Contains("command_id", required);
        Assert.Contains("member_episode_id", required);
        Assert.Contains("expected_member_revision", required);
    }

    static async Task<Uuid> RegisterAsync(WebApplicationFactory<Program> factory, Uuid tenant, Uuid user, bool suspended)
    {
        var episode = Uuid.CreateVersion4();
        await using var scope = factory.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var system = new RequestDispatchContext(RequestActor.System);
        Assert.True((await executor.ExecuteAsync(new Member(tenant, user), member =>
            AggregateOutcome.CommitOnSuccess(member.Register(commandId: episode)), system, CancellationToken.None)).IsSuccess);
        if (suspended)
            Assert.True((await executor.ExecuteAsync(new Member(tenant, user), member =>
                AggregateOutcome.CommitOnSuccess(member.Suspend(Uuid.CreateVersion4(), "Alex Admin",
                    DateTimeOffset.UtcNow, "Employment ended.")), system, CancellationToken.None)).IsSuccess);
        return episode;
    }

    static async Task<List<DomainEvent>> GuardFactsAsync(WebApplicationFactory<Program> factory, Uuid tenant)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new TenantManagerGuard(tenant).Stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"member-transition-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
