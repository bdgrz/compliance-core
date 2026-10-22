using System.Net;
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
public sealed class TeamRoleTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AnotherId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveLaterAuthorityAndLogicalIdentityGivenHttpOrMcpRetries(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var role = BuiltInRbac.ComplianceManagementRoleId(tenant);
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        await SendAsync(first, 0);
        await SendAsync(removal, 1, first);

        // Act
        await SendAsync(first, 0);
        await SendAsync(next, 2);
        await SendAsync(removal, 1, first);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(tenant, team, role));
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>().ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);

        // Assert
        Assert.Equal(next, owner.AssignmentId);
        Assert.Equal(3UL, owner.Version);
        Assert.Equal(3, facts.Count);
        Assert.Collection(facts,
            fact => AssertAccepted(first, Assert.IsType<TeamRoleAssigned>(fact).CommandId, fact),
            fact => AssertAccepted(removal, Assert.IsType<TeamRoleRemoved>(fact).CommandId, fact),
            fact => AssertAccepted(next, Assert.IsType<TeamRoleAssigned>(fact).CommandId, fact));

        async Task SendAsync(Uuid commandId, ulong revision, Uuid? assignment = null)
        {
            if (scenario is not null)
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.ToString(),
                    ["team_id"] = team.ToString(),
                    ["role_id"] = role.ToString(),
                    ["command_id"] = commandId.ToString(),
                    ["expected_revision"] = revision,
                };
                if (assignment is not null)
                    arguments["assignment_id"] = assignment.Value.ToString();
                await scenario.When(assignment is null ? "bdgrz.rbac.team-role.assign" : "bdgrz.rbac.team-role.remove", arguments)
                    .ExpectSuccess();
                return;
            }
            using var request = new HttpRequestMessage(assignment is null ? HttpMethod.Put : HttpMethod.Delete,
                $"/api/v1/tenants/{tenant}/teams/{team}/roles/{role}/{(assignment is null ? "assignments" : "removals")}/{commandId}" +
                (assignment is null ? "" : $"?expected_revision={revision}&assignment_id={assignment}"));
            if (assignment is null)
                request.Content = new StringContent($"{{\"expected_revision\":{revision}}}", Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("", "", HttpStatusCode.NotFound)]
    [InlineData("malformed", "", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", "", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, "", HttpStatusCode.NoContent)]
    [InlineData(OperationId, ",\"command_id\":\"" + OperationId + "\"", HttpStatusCode.NoContent)]
    [InlineData(OperationId, ",\"command_id\":\"" + AnotherId + "\"", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, ",\"command_id\":null", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, ",\"command_id\":\"malformed\"", HttpStatusCode.BadRequest)]
    public async Task ShouldValidateRequiredRouteIdentityGivenAnAuthorizedHttpCommand(
        string operationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var role = BuiltInRbac.ComplianceManagementRoleId(tenant);
        using var body = new StringContent("{\"expected_revision\":0" + duplicate + "}", Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/teams/{team}/roles/{role}/assignments/{operationId}", body);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(tenant, team, role));

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.NoContent ? 1UL : 0UL, owner.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ShouldRejectMissingOrInvalidTypedIdentityGivenMcpInput(string? commandId)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var tenant = Uuid.CreateVersion4();
        var arguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenant.ToString(),
            ["team_id"] = Uuid.CreateVersion4().ToString(),
            ["role_id"] = BuiltInRbac.ComplianceManagementRoleId(tenant).ToString(),
            ["expected_revision"] = 0,
        };
        if (commandId is not null)
            arguments["command_id"] = commandId;

        // Act
        var tools = await scenario.ListTools();
        var result = await scenario.When("bdgrz.rbac.team-role.assign", arguments);

        // Assert
        var tool = Assert.Single(tools, item => item.Name == "bdgrz.rbac.team-role.assign");
        Assert.Contains("command_id", tool.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.IsError);
    }

    static void AssertAccepted(Uuid expected, Uuid accepted, DomainEvent fact)
    {
        Assert.Equal(expected, accepted);
        Assert.Equal(expected, fact.Metadata.CausationId);
        var actor = fact is TeamRoleAssigned assigned ? assigned.Actor : ((TeamRoleRemoved)fact).Actor;
        Assert.Equal(actor, fact.Metadata.Actor);
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"team-role-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
