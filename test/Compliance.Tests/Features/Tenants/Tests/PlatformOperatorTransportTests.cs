using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
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
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

[Trait("Category", "WebIntegration")]
public sealed class PlatformOperatorTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AnotherId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Fact]
    public async Task ShouldReturnDeclaredUnitBodyGivenPlatformGrantHttpSuccessAndRetry()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var command = Uuid.CreateVersion4();
        var body = new { user_id = Uuid.CreateVersion4(), expected_revision = 1, reason = "Cover operations" };
        var route = $"/api/v1/platform/operator-grants/{command}";

        // Act
        using var first = await client.PutAsJsonAsync(route, body);
        using var retry = await client.PutAsJsonAsync(route, body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("{}", await first.Content.ReadAsStringAsync());
        Assert.Equal(await first.Content.ReadAsByteArrayAsync(), await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/json", first.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveLaterAssignmentsAndLogicalIdentityGivenHttpOrMcpRetries(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var actor = await LoginAndSeedAsync(factory, client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var target = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var removal = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        await SendAsync(first, 1, "Cover operations");
        await SendAsync(removal, 2, "Coverage ended", first);

        // Act
        await SendAsync(first, 1, "Cover operations");
        await SendAsync(next, 3, "New coverage");
        await SendAsync(removal, 2, "Coverage ended", first);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>().ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);
        using var response = await client.GetAsync("/api/v1/platform/operators");
        var view = await response.Content.ReadFromJsonAsync(ComplianceCoreJsonContext.Default.PlatformOperatorRosterView);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authority = await PlatformOperatorRosterContent.ReadAsync(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), owner);
        Assert.Equal(next, Assert.Single(authority.Operators, item => item.UserId == target).AssignmentId);
        Assert.Equal(4UL, owner.Version);
        Assert.Equal(4UL, view!.Revision);
        Assert.Equal(next, Assert.Single(view.Operators, entry => entry.UserId == target).AssignmentId);
        Assert.Collection(facts,
            fact => Assert.Equal(PlatformOperatorRoster.CreationId, fact.Metadata.CausationId),
            fact => AssertAccepted(first, actor, Assert.IsType<PlatformOperatorGranted>(fact)),
            fact =>
            {
                var revoked = Assert.IsType<PlatformOperatorRevoked>(fact);
                Assert.Equal(removal, revoked.CommandId);
                Assert.Equal(removal, fact.Metadata.CausationId);
                Assert.Equal(first, revoked.AssignmentId);
                Assert.Equal(revoked.Actor, fact.Metadata.Actor);
            },
            fact => AssertAccepted(next, actor, Assert.IsType<PlatformOperatorGranted>(fact)));

        async Task SendAsync(Uuid commandId, ulong revision, string reason, Uuid? assignment = null)
        {
            var arguments = new Dictionary<string, object?>
            {
                ["user_id"] = target.ToString(),
                ["expected_revision"] = revision,
                ["reason"] = reason,
            };
            if (assignment is not null)
                arguments["assignment_id"] = assignment.Value.ToString();
            if (scenario is not null)
            {
                arguments["command_id"] = commandId.ToString();
                await scenario.When(assignment is null ? "bdgrz.platform.operator.grant" : "bdgrz.platform.operator.revoke", arguments)
                    .ExpectSuccess();
                return;
            }
            using var result = await client.PutAsJsonAsync(
                $"/api/v1/platform/{(assignment is null ? "operator-grants" : "operator-revocations")}/{commandId}", arguments);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
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
    public async Task ShouldBindOrRejectRequiredOperationIdentityGivenHttpInput(
        string operationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var target = Uuid.CreateVersion4();
        using var body = new StringContent("{\"user_id\":\"" + target + "\",\"reason\":\"Cover operations\",\"expected_revision\":1" + duplicate + "}",
            Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PutAsync($"/api/v1/platform/operator-grants/{operationId}", body);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperatorRoster());

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 2UL : 1UL, owner.Version);
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
        await LoginAndSeedAsync(factory, client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var arguments = new Dictionary<string, object?>
        {
            ["user_id"] = Uuid.CreateVersion4().ToString(),
            ["reason"] = "Cover operations",
            ["expected_revision"] = 1,
        };
        if (commandId is not null)
            arguments["command_id"] = commandId;

        // Act
        var tools = await scenario.ListTools();
        var result = await scenario.When("bdgrz.platform.operator.grant", arguments);

        // Assert
        var tool = Assert.Single(tools, item => item.Name == "bdgrz.platform.operator.grant");
        Assert.Contains("command_id", tool.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ShouldPreserveAndValidateTheTypedIdentityGivenAQueueEnvelope()
    {
        // Arrange
        await using var factory = Factory();
        var options = factory.Services.GetRequiredKeyedService<JsonSerializerOptions>(PortiaServiceKeys.Json);
        var serializer = new JsonRequestSerializer([
            new RequestTransportRegistration(typeof(RevokePlatformOperator), [RequestTransportId.Queue],
                new RequestRouteAttribute("bdgrz", "operators", "roster", "revoke"),
                new DiscriminatorAttribute("bdgrz.platform.operator.revoke", 1)),
        ], options);
        var command = new RevokePlatformOperator(Uuid.CreateVersion4(), "Coverage ended", Uuid.CreateVersion4(), 7, Uuid.CreateVersion4());
        var metadata = RequestMetadata.FromRequest(command);

        // Act
        var bytes = serializer.Serialize(command, null, metadata, null);
        var envelope = serializer.DeserializeEnvelope(bytes);
        var tampered = JsonNode.Parse(bytes.Span)!;
        var payload = tampered["payload"]!.AsObject();
        var key = Assert.Single(payload.Select(pair => pair.Key), name => name is "command_id" or "commandId" or "CommandId");
        payload[key] = Uuid.CreateVersion4().ToString();

        // Assert
        Assert.Equal(command, Assert.IsType<RevokePlatformOperator>(envelope.Request));
        Assert.Equal(command.CommandId, envelope.Metadata.RequestId);
        Assert.Throws<InvalidOperationException>(() => serializer.DeserializeEnvelope(Encoding.UTF8.GetBytes(tampered.ToJsonString())));
    }

    [Fact]
    public async Task ShouldReturnOriginalUnitBodyGivenNativeLifecycleHttpRetry()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var actor = await LoginAndSeedAsync(factory, client);
        var tenantId = Uuid.CreateVersion4();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var tenant = new Tenant(tenantId);
            Assert.True(tenant.Register(actor, "Company", "lifecycle-company").IsSuccess);
            Assert.True(tenant.ConfirmSlug("lifecycle-company").IsSuccess);
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(tenant,
                new RequestDispatchContext(RequestActor.System));
        }
        var id = Uuid.CreateVersion4();
        var body = new Dictionary<string, object> { ["reason"] = "Platform decision", ["expected_revision"] = 2 };
        var path = $"/api/v1/tenants/{tenantId}/suspensions/{id}";

        // Act
        using var first = await client.PutAsJsonAsync(path, body);
        using var replay = await client.PutAsJsonAsync(path, body);
        await using var verification = factory.Services.CreateAsyncScope();
        var current = await verification.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Tenant(tenantId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("{}", await first.Content.ReadAsStringAsync());
        Assert.Equal("{}", await replay.Content.ReadAsStringAsync());
        Assert.True(current.IsSuspended);
        Assert.Equal(3UL, current.Version);
    }

    static void AssertAccepted(Uuid commandId, Uuid actor, PlatformOperatorGranted fact)
    {
        Assert.Equal(commandId, fact.CommandId);
        Assert.Equal(commandId, fact.Metadata.CausationId);
        Assert.Equal(actor, fact.ActorUserId);
        Assert.Equal(fact.Actor, fact.Metadata.Actor);
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlatformUserDirectoryReader>();
            services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
        }));

    static async Task<Uuid> LoginAndSeedAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var actor = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "operator-transport@example.com"), CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, [actor]), RequestActor.System)).IsSuccess);
        return actor;
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
