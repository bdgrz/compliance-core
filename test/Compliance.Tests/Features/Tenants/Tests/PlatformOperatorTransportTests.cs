using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

[Trait("Category", "WebIntegration")]
public sealed class PlatformOperatorTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AnotherId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreservePerUserEpisodeGivenExactHttpOrMcpRetries(bool mcp)
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
        await SendAsync(first, 0, "Cover operations");
        await SendAsync(removal, 1, "Coverage ended", first);

        // Act
        await SendAsync(first, 0, "Cover operations");
        await SendAsync(next, 2, "New coverage");
        await SendAsync(removal, 1, "Coverage ended", first);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new PlatformOperator(target));
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>().ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);
        await ProjectAsync(factory, actor, target);
        using var response = await client.GetAsync("/api/v1/platform/operators?limit=1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(owner.IsActive);
        Assert.Equal(next, owner.GrantEpisodeId);
        Assert.Equal(3UL, owner.Version);
        Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("next_cursor").ValueKind);
        Assert.Collection(facts,
            fact => Assert.Equal(first, Assert.IsType<PlatformOperatorAccessGranted>(fact).OperationId),
            fact => Assert.Equal(first, Assert.IsType<PlatformOperatorAccessRevoked>(fact).GrantEpisodeId),
            fact => Assert.Equal(next, Assert.IsType<PlatformOperatorAccessGranted>(fact).OperationId));

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

    [Fact]
    public async Task ShouldRejectChangedIntentGivenReusedHttpOperationIdentity()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var target = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var path = $"/api/v1/platform/operator-grants/{operation}";
        using var first = await client.PutAsJsonAsync(path, new { user_id = target, expected_revision = 0, reason = "First" });

        // Act
        using var changed = await client.PutAsJsonAsync(path, new { user_id = target, expected_revision = 0, reason = "Changed" });
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(target));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(operation, owner.GrantEpisodeId);
    }

    [Fact]
    public async Task ShouldForbidOperatorReadsAndWritesGivenAuthenticatedNonOperator()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(client, "not-an-operator@example.com");
        var target = Uuid.CreateVersion4();

        // Act
        using var list = await client.GetAsync("/api/v1/platform/operators");
        using var state = await client.GetAsync($"/api/v1/platform/operators/{target}");
        using var grant = await client.PutAsJsonAsync($"/api/v1/platform/operator-grants/{Uuid.CreateVersion4()}",
            new { user_id = target, expected_revision = 0, reason = "Unauthorized" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, state.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(target));
        Assert.Equal(0UL, owner.Version);
    }

    [Theory]
    [InlineData("", "", HttpStatusCode.NotFound)]
    [InlineData("malformed", "", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", "", HttpStatusCode.BadRequest)]
    [InlineData(OperationId, "", HttpStatusCode.OK)]
    [InlineData(OperationId, ",\"command_id\":\"" + OperationId + "\"", HttpStatusCode.OK)]
    [InlineData(OperationId, ",\"command_id\":\"" + AnotherId + "\"", HttpStatusCode.BadRequest)]
    public async Task ShouldBindOrRejectOperationIdentityGivenHttpInput(string operationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAndSeedAsync(factory, client);
        var target = Uuid.CreateVersion4();
        using var body = new StringContent("{\"user_id\":\"" + target + "\",\"reason\":\"Cover operations\",\"expected_revision\":0" + duplicate + "}",
            Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PutAsync($"/api/v1/platform/operator-grants/{operationId}", body);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(target));

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? 1UL : 0UL, owner.Version);
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
            ["expected_revision"] = 0,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExposeOwnerRevisionGivenAuthorizedStateRead(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var user = await LoginAndSeedAsync(factory, client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        JsonElement state;
        if (scenario is not null)
        {
            var response = await scenario.When("bdgrz.platform.operator.state.get", new Dictionary<string, object?>
            { ["user_id"] = user.ToString() }).ExpectSuccess();
            state = Assert.IsType<JsonElement>(response.StructuredJson).GetProperty("result");
        }
        else
        {
            using var response = await client.GetAsync($"/api/v1/platform/operators/{user}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            state = document.RootElement.Clone();
        }

        // Assert
        Assert.Equal(user.ToString(), state.GetProperty("user_id").GetString());
        Assert.True(state.GetProperty("is_active").GetBoolean());
        Assert.Equal(1UL, state.GetProperty("operator_revision").GetUInt64());
        Assert.Equal(0UL, state.GetProperty("primary_revision").GetUInt64());
        Assert.Equal(0UL, state.GetProperty("seat_generation").GetUInt64());
    }

    [Fact]
    public async Task ShouldRejectOperatorToolsGivenAuthenticatedNonOperator()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(client, "not-an-operator-mcp@example.com");
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var user = Uuid.CreateVersion4();

        // Act
        var state = await scenario.When("bdgrz.platform.operator.state.get", new Dictionary<string, object?>
        { ["user_id"] = user.ToString() });
        var grant = await scenario.When("bdgrz.platform.operator.grant", new Dictionary<string, object?>
        {
            ["user_id"] = user.ToString(),
            ["command_id"] = Uuid.CreateVersion4().ToString(),
            ["expected_revision"] = 0,
            ["reason"] = "Unauthorized",
        });

        // Assert
        Assert.True(state.IsError);
        Assert.True(grant.IsError);
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new PlatformOperator(user));
        Assert.Equal(0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldPreserveAndValidateTypedIdentityGivenQueueEnvelope()
    {
        // Arrange
        await using var factory = Factory();
        var options = factory.Services.GetRequiredKeyedService<JsonSerializerOptions>(PortiaServiceKeys.Json);
        var serializer = new JsonRequestSerializer([
            new RequestTransportRegistration(typeof(RevokePlatformOperator), [RequestTransportId.Queue],
                new RequestRouteAttribute("bdgrz", "operators", "user", "revoke"),
                new DiscriminatorAttribute("bdgrz.platform.operator.revoke", 1)),
        ], options);
        var command = new RevokePlatformOperator(Uuid.CreateVersion4(), "Coverage ended", Uuid.CreateVersion4(), 7, Uuid.CreateVersion4());

        // Act
        var bytes = serializer.Serialize(command, null, RequestMetadata.FromRequest(command), null);
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

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlatformUserDirectoryReader>();
            services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
            services.RemoveAll<IPlatformOperatorAccess>();
            services.AddScoped<IPlatformOperatorAccess, StoredOperatorAccess>();
        }));

    static async Task<Uuid> LoginAndSeedAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var actor = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "operator-transport@example.com"), CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(actor, operation);
        Assert.True(owner.GrantBootstrap(operation, 0, Uuid.CreateVersion4(), new ActorAttribution("portia:system", "Portia")).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(owner,
            new RequestDispatchContext(RequestActor.System));
        return actor;
    }

    static async Task ProjectAsync(WebApplicationFactory<Program> factory, params Uuid[] users)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var projection = scope.ServiceProvider.GetRequiredService<IPlatformOperatorDirectoryProjection>();
        var identity = new CheckpointIdentity("PlatformOperatorDirectory", EventStreamPattern.ForPattern("bdgrz", "platform-operators"));
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var events = scope.ServiceProvider.GetRequiredService<IEventStore>();
        foreach (var user in users)
            await foreach (var record in events.ReadAsync(PlatformOperator.AddressOf(user), 0))
                await projection.ApplyAsync(record.Event);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StoredOperatorAccess(IAggregateReader reader) : IPlatformOperatorAccess
    {
        public async ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            userId != Uuid.Empty && (await reader.HydrateAsync(new PlatformOperator(userId), ct)).IsActive;
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
