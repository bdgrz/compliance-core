using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "WebIntegration")]
public sealed class TeamMemberTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AnotherId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayCompletedResponseAndAppendOnceGivenHttpOrMcpRetries(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, UserId);
        var episode = await RegisterAsync(factory, tenant);
        var assignment = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        await SendAsync(assignment, false, 0, 0);
        await SendAsync(removal, true, 1, 1);

        // Act
        await SendAsync(assignment, false, 0, 0);
        await SendAsync(removal, true, 1, 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamMember(tenant, team, member));
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>().ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);

        // Assert
        Assert.False(owner.IsAssigned);
        Assert.Equal(2UL, owner.Version);
        Assert.Collection(facts,
            fact => Assert.Equal(assignment, Assert.IsType<TeamMemberAssigned>(fact).CommandId),
            fact => Assert.Equal(removal, Assert.IsType<TeamMemberRemoved>(fact).CommandId));

        async Task SendAsync(Uuid commandId, bool remove, long revision, long generation)
        {
            if (scenario is not null)
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.ToString(),
                    ["team_id"] = team.ToString(),
                    ["member_id"] = member.ToString(),
                    ["command_id"] = commandId.ToString(),
                    ["member_episode_id"] = episode.ToString(),
                    ["expected_assignment_revision"] = revision,
                    ["expected_assignment_generation"] = generation,
                };
                await scenario.When(remove ? "bdgrz.rbac.team-member.remove" : "bdgrz.rbac.team-member.assign", arguments)
                    .ExpectSuccess();
                return;
            }
            using var response = await client.PutAsJsonAsync(remove
                    ? $"/api/v1/tenants/{tenant}/members/{member}/team-removals/{commandId}"
                    : $"/api/v1/tenants/{tenant}/teams/{team}/members/{member}/{commandId}",
                new
                {
                    team_id = team,
                    member_episode_id = episode,
                    expected_assignment_revision = revision,
                    expected_assignment_generation = generation,
                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(commandId.ToString(), document.RootElement.GetProperty("operation_id").GetString());
            Assert.Equal("completed", document.RootElement.GetProperty("state").GetString());
        }
    }

    [Theory]
    [InlineData("", "", HttpStatusCode.NotFound)]
    [InlineData("malformed", "", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", "", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, "", HttpStatusCode.OK)]
    [InlineData(OperationId, ",\"command_id\":\"" + OperationId + "\"", HttpStatusCode.OK)]
    [InlineData(OperationId, ",\"command_id\":\"" + AnotherId + "\"", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, ",\"command_id\":null", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, ",\"command_id\":\"malformed\"", HttpStatusCode.BadRequest)]
    public async Task ShouldValidateRequiredRouteIdentityGivenAnAuthorizedHttpAssignment(
        string operationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var team = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, UserId);
        var episode = await RegisterAsync(factory, tenant);
        using var body = new StringContent("{\"member_episode_id\":\"" + episode +
            "\",\"expected_assignment_revision\":0,\"expected_assignment_generation\":0" + duplicate + "}",
            Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/teams/{team}/members/{member}/{operationId}", body);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamMember(tenant, team, member));

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1UL : 0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldRetireTheOldMemberRoutesGivenIdentifiedTeamMemberCommands()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/teams/{Uuid.CreateVersion4()}/members/{RbacIds.Member(tenant, UserId)}";

        // Act
        using var post = await client.PostAsJsonAsync(path, new { });
        using var delete = await client.DeleteAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Theory]
    [InlineData("bdgrz.rbac.team-member.assign", null)]
    [InlineData("bdgrz.rbac.team-member.assign", "malformed")]
    [InlineData("bdgrz.rbac.team-member.assign", "00000000-0000-0000-0000-000000000000")]
    [InlineData("bdgrz.rbac.team-member.remove", null)]
    [InlineData("bdgrz.rbac.team-member.remove", "malformed")]
    public async Task ShouldRejectMissingOrInvalidTypedIdentityGivenMcpInput(string tool, string? commandId)
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
            ["member_id"] = RbacIds.Member(tenant, UserId).ToString(),
            ["member_episode_id"] = Uuid.CreateVersion4().ToString(),
            ["expected_assignment_revision"] = 0,
            ["expected_assignment_generation"] = 0,
        };
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

    static readonly Uuid UserId = Uuid.Parse("a1f455d2-fb10-4f28-a157-23e18e706e71", System.Globalization.CultureInfo.InvariantCulture);

    static async Task<Uuid> RegisterAsync(WebApplicationFactory<Program> factory, Uuid tenant)
    {
        var episode = Uuid.CreateVersion4();
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new Member(tenant, UserId), member => AggregateOutcome.CommitOnSuccess(member.Register(commandId: episode)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return episode;
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"team-member-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
