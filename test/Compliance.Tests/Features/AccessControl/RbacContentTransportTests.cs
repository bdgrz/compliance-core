using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class RbacContentTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayRetriesAndConflictOnChangedContentGivenHttpOrMcpTeamOperations(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var deletion = Uuid.CreateVersion4();

        // Act
        await DefineAsync("Reviewers", succeeds: true);
        await DefineAsync("Reviewers", succeeds: true);
        await DefineAsync("Auditors", succeeds: false);
        await DeleteAsync(deletion);
        await DeleteAsync(deletion);
        await DefineAsync("Reviewers", succeeds: true);
        var facts = await FactsAsync(factory, new Team(tenant, team).Stream);

        // Assert
        var defined = Assert.Single(facts.OfType<TeamDefined>());
        Assert.Equal("Reviewers", defined.Name);
        Assert.Equal(team, defined.CommandId);
        var deleted = Assert.Single(facts.OfType<TeamDeleted>());
        Assert.Equal(deletion, deleted.CommandId);

        async Task DefineAsync(string name, bool succeeds)
        {
            if (scenario is not null)
            {
                var call = scenario.When("bdgrz.rbac.team.define", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.ToString(),
                    ["team_id"] = team.ToString(),
                    ["name"] = name,
                });
                if (succeeds)
                    await call.ExpectSuccess();
                else
                    await call.ExpectFailure();
                return;
            }
            using var response = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant}/teams/{team}", new { name });
            Assert.Equal(succeeds ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, response.StatusCode);
        }

        async Task DeleteAsync(Uuid commandId)
        {
            if (scenario is not null)
            {
                await scenario.When("bdgrz.rbac.team.delete", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.ToString(),
                    ["team_id"] = team.ToString(),
                    ["command_id"] = commandId.ToString(),
                }).ExpectSuccess();
                return;
            }
            using var request = new HttpRequestMessage(HttpMethod.Put,
                $"/api/v1/tenants/{tenant}/teams/{team}/deletions/{commandId}")
            { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("", HttpStatusCode.NotFound)]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, HttpStatusCode.NoContent)]
    public async Task ShouldValidateRequiredRouteIdentityGivenAnAuthorizedRoleDeletion(
        string commandId, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var seeded = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
                new Role(tenant, role), item => AggregateOutcome.CommitOnSuccess(item.Define("Reviewer")),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(seeded.IsSuccess);
        }

        // Act
        using var response = await client.PutAsync($"/api/v1/tenants/{tenant}/roles/{role}/deletions/{commandId}",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var facts = await FactsAsync(factory, new Role(tenant, role).Stream);

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.NoContent ? 1 : 0, facts.OfType<RoleDeleted>().Count());
    }

    [Fact]
    public async Task ShouldRetireTheOldMethodsGivenIdentifiedTeamRoleAndSlugOperations()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var team = $"/api/v1/tenants/{tenant}/teams/{Uuid.CreateVersion4()}";
        var role = $"/api/v1/tenants/{tenant}/roles/{Uuid.CreateVersion4()}";

        // Act
        using var postTeam = await client.PostAsJsonAsync(team, new { name = "Reviewers" });
        using var deleteTeam = await client.DeleteAsync(team);
        using var postRole = await client.PostAsJsonAsync(role, new { name = "Reviewer" });
        using var deleteRole = await client.DeleteAsync(role);
        using var postSlug = await client.PostAsJsonAsync($"/api/v1/tenants/{tenant}/slug-changes", new { slug = "acme-next" });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, postTeam.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteTeam.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, postRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, postSlug.StatusCode);
    }

    [Theory]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    public async Task ShouldValidateTheSlugChangeIdentityGivenAMalformedRoute(string commandId, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        using var body = new StringContent("{\"slug\":\"acme-next\",\"expected_revision\":0}", Encoding.UTF8,
            "application/json");

        // Act
        using var response = await client.PutAsync($"/api/v1/tenants/{Uuid.CreateVersion4()}/slug-changes/{commandId}", body);

        // Assert
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("bdgrz.rbac.team.delete", null)]
    [InlineData("bdgrz.rbac.team.delete", "malformed")]
    [InlineData("bdgrz.rbac.role.delete", null)]
    [InlineData("bdgrz.rbac.role.delete", "00000000-0000-0000-0000-000000000000")]
    [InlineData("bdgrz.tenant.change-slug", null)]
    public async Task ShouldRejectMissingOrInvalidTypedIdentityGivenMcpInput(string tool, string? commandId)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var arguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = Uuid.CreateVersion4().ToString(),
        };
        if (tool.Contains("team", StringComparison.Ordinal))
            arguments["team_id"] = Uuid.CreateVersion4().ToString();
        else if (tool.Contains("role", StringComparison.Ordinal))
            arguments["role_id"] = Uuid.CreateVersion4().ToString();
        else
        {
            arguments["slug"] = "acme-next";
            arguments["expected_revision"] = 0;
        }
        if (commandId is not null)
            arguments["command_id"] = commandId;

        // Act
        var tools = await scenario.ListTools();
        var result = await scenario.When(tool, arguments);

        // Assert
        var listed = Assert.Single(tools, item => item.Name == tool);
        Assert.Contains("command_id", listed.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.IsError);
    }

    static async Task<List<DomainEvent>> FactsAsync(WebApplicationFactory<Program> factory, EventStreamAddress stream)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>().ReadAsync(stream, 0))
            facts.Add(record.Event);
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
        using var body = new StringContent("{\"email_address\":\"rbac-content-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
