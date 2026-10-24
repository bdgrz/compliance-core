using System.Net;
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
public sealed class ServiceEngagementTransportTests
{
    [Fact]
    public async Task ShouldReplayExactBodyAndConflictOnChangeGivenEveryLifecycleRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var lead = await FirmStaffFacts.RegisterAsync(factory.Services, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "advisory", "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Op"),
            IndependenceHost.At);
        var other = await FirmStaffFacts.RegisterAsync(factory.Services, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "advisory", "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Op"),
            IndependenceHost.At);
        var engagement = Uuid.CreateVersion4();
        var basePath = $"/api/v1/tenants/{tenant}/service-engagements/{engagement}";
        var amend = Uuid.CreateVersion4();
        var propose = Uuid.CreateVersion4();
        var withdraw = Uuid.CreateVersion4();
        var close = Uuid.CreateVersion4();

        // Act
        using var created = await client.PutAsync($"{basePath}/create-service-engagement", Draft(lead.StaffMemberId, 0));
        using var createdAgain = await client.PutAsync($"{basePath}/create-service-engagement", Draft(lead.StaffMemberId, 0));
        using var createChanged = await client.PutAsync($"{basePath}/create-service-engagement",
            Draft(lead.StaffMemberId, 0, "Another scope"));
        using var amended = await client.PutAsync($"{basePath}/amend-service-engagement/{amend}",
            Draft(lead.StaffMemberId, 1, "Revised"));
        using var proposed = await client.PutAsync($"{basePath}/propose-service-engagement-staff/{propose}",
            Json("{\"staff_member_id\":\"" + other.StaffMemberId + "\",\"expected_revision\":2}"));
        using var proposedAgain = await client.PutAsync($"{basePath}/propose-service-engagement-staff/{propose}",
            Json("{\"staff_member_id\":\"" + other.StaffMemberId + "\",\"expected_revision\":2}"));
        using var withdrawn = await client.PutAsync(
            $"{basePath}/staff-proposals/{other.StaffMemberId}/withdrawals/{withdraw}",
            Json("{\"expected_revision\":3,\"reason\":\"Synthetic withdrawal\"}"));
        using var closed = await client.PutAsync($"{basePath}/closure/{close}",
            Json("{\"expected_revision\":4,\"reason\":\"Synthetic closure\"}"));
        using var closedAgain = await client.PutAsync($"{basePath}/closure/{close}",
            Json("{\"expected_revision\":4,\"reason\":\"Synthetic closure\"}"));
        using var staleClose = await client.PutAsync($"{basePath}/closure/{Uuid.CreateVersion4()}",
            Json("{\"expected_revision\":4,\"reason\":\"Synthetic closure\"}"));
        using var history = await client.GetAsync($"{basePath}/history");
        using var oldCreate = await client.PutAsync(basePath, Draft(lead.StaffMemberId, 0));
        using var oldDraft = await client.PutAsync($"{basePath}/draft", Draft(lead.StaffMemberId, 1));
        using var oldClosure = await client.PostAsync($"{basePath}/closure", Json("{\"expected_revision\":5,\"reason\":\"x\"}"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await createdAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, createChanged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, amended.StatusCode);
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        Assert.Equal(await proposed.Content.ReadAsStringAsync(), await proposedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(await closed.Content.ReadAsStringAsync(), await closedAgain.Content.ReadAsStringAsync());
        using var view = JsonDocument.Parse(await closedAgain.Content.ReadAsStringAsync());
        Assert.Equal(5, view.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("closed", view.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, staleClose.StatusCode);
        using var revisions = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(5, revisions.RootElement.GetArrayLength());
        Assert.True(oldCreate.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.True(oldDraft.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.True(oldClosure.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("malformed")]
    public async Task ShouldRejectGivenMissingOrMalformedRouteIdentity(string identity)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/service-engagements/{engagement}";

        // Act
        using var create = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{identity}/create-service-engagement",
            Draft(Uuid.CreateVersion4(), 0));
        using var amend = await client.PutAsync($"{path}/amend-service-engagement/{identity}",
            Draft(Uuid.CreateVersion4(), 1));
        using var close = await client.PutAsync($"{path}/closure/{identity}",
            Json("{\"expected_revision\":1,\"reason\":\"x\"}"));
        using var service = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/nonattest-services/record-nonattest-service/{identity}", Service(0));
        using var evaluate = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/client-independence-evaluations/evaluate-client-independence/{identity}",
            Json("{\"rule_set_version\":1,\"examination_period_start\":\"2026-10-07\",\"expected_service_epoch\":0}"));

        // Assert
        Assert.All(new[] { create, amend, close, service, evaluate },
            response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
    }

    [Fact]
    public async Task ShouldReturnOriginalServiceViewGivenRetryAfterLaterServiceAndRejectOldRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/nonattest-services/record-nonattest-service";

        // Act
        using var recorded = await client.PutAsync($"{path}/{first}", Service(0));
        using var later = await client.PutAsync($"{path}/{Uuid.CreateVersion4()}", Service(1));
        using var retried = await client.PutAsync($"{path}/{first}", Service(0));
        using var changed = await client.PutAsync($"{path}/{first}", Service(0, "A different source"));
        using var stale = await client.PutAsync($"{path}/{Uuid.CreateVersion4()}", Service(1));
        using var oldRoute = await client.PutAsync($"/api/v1/tenants/{tenant}/independence/services/{first}", Service(0));

        // Assert
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(await recorded.Content.ReadAsStringAsync(), await retried.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
    }

    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenEvaluateRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await ProgramManagementServices.SeedAsync(factory.Services, new IndependenceRuleCatalog(), catalog =>
            catalog.ReviseRules(Uuid.CreateVersion4(), 0, IndependenceHost.DefaultRules,
                ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), IndependenceHost.At));
        var tenant = Uuid.CreateVersion4();
        var evaluation = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/client-independence-evaluations/evaluate-client-independence/{evaluation}";
        const string body = "{\"rule_set_version\":1,\"examination_period_start\":\"2026-10-07\",\"expected_service_epoch\":0}";

        // Act
        using var first = await client.PutAsync(path, Json(body));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Json(body));
        var retryText = await retried.Content.ReadAsStringAsync();
        using var changed = await client.PutAsync(path, Json(body.Replace("2026-10-07", "2026-10-08", StringComparison.Ordinal)));
        using var oldRoute = await client.PutAsync($"/api/v1/tenants/{tenant}/independence/evaluations/{evaluation}",
            Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(evaluation.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/tenants/{tenant}/operations/{evaluation}",
            document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal(document.RootElement.GetProperty("status_url").GetString(), first.Headers.Location?.OriginalString);
        Assert.True(document.RootElement.TryGetProperty("accepted_at", out _));
    }

    [Fact]
    public async Task ShouldFindNothingToRevokeGivenUnknownEngagementOnRevocationRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/service-engagements/{Uuid.CreateVersion4()}/actual-assignments/" +
                   $"{Uuid.CreateVersion4()}/revocations/{Uuid.CreateVersion4()}";

        // Act
        using var response = await client.PutAsync(path, Json("{\"expected_revision\":1,\"reason\":\"Synthetic\"}"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldKeepUnregisteredPersonalDecisionAndAcceptanceRoutesClosedGivenIndependenceSlice()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var path = $"/api/v1/tenants/{Uuid.CreateVersion4()}/service-engagements/{Uuid.CreateVersion4()}";

        // Act
        using var accept = await client.PostAsync($"{path}/acceptance", Json("{}"));
        using var assign = await client.PostAsync($"{path}/actual-assignments/{Uuid.CreateVersion4()}", Json("{}"));
        using var partner = await client.PostAsync($"{path}/partner-evaluations", Json("{}"));
        using var acknowledge = await client.PostAsync($"{path}/management-acknowledgements/{Uuid.CreateVersion4()}",
            Json("{}"));
        using var context = await client.GetAsync($"{path}/partner-review-context");

        // Assert
        Assert.All(new[] { accept, assign, partner, acknowledge, context },
            response => Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                response.StatusCode.ToString()));
    }

    [Fact]
    public async Task ShouldExposeRequiredIdentitiesAndOmitUnregisteredToolsGivenMcpTools()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        // Act
        var tools = await scenario.ListTools();
        var missingCommand = await scenario.When("bdgrz.service-engagement.close", new Dictionary<string, object?>
        {
            ["tenant_id"] = Uuid.CreateVersion4().ToString(),
            ["engagement_id"] = Uuid.CreateVersion4().ToString(),
            ["expected_revision"] = 1,
            ["reason"] = "Synthetic",
        });

        // Assert
        string[] RequiredOf(string name) => Assert.Single(tools, item => item.Name == name).InputSchema
            .GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Contains("command_id", RequiredOf("bdgrz.service-engagement.amend"));
        Assert.Contains("command_id", RequiredOf("bdgrz.service-engagement.close"));
        Assert.Contains("command_id", RequiredOf("bdgrz.service-engagement.staff.propose"));
        Assert.Contains("command_id", RequiredOf("bdgrz.service-engagement.staff.withdraw"));
        Assert.Contains("command_id", RequiredOf("bdgrz.service-engagement.actual-staff.revoke"));
        Assert.Contains("engagement_id", RequiredOf("bdgrz.service-engagement.create"));
        Assert.Contains("service_record_id", RequiredOf("bdgrz.independence.service.record"));
        Assert.Contains("evaluation_id", RequiredOf("bdgrz.independence.evaluate"));
        Assert.Contains("expected_service_epoch", RequiredOf("bdgrz.independence.evaluate"));
        Assert.True(missingCommand.IsError);
        Assert.DoesNotContain(tools, item => item.Name is "bdgrz.service-engagement.accept" or
            "bdgrz.access-control.assign-service-engagement-staff" or "bdgrz.service-engagement.management-acknowledge" or
            "bdgrz.service-engagement.partner-evaluation.record" or "bdgrz.service-engagement.partner-review-context.get");
    }

    static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    static StringContent Draft(Uuid lead, int revision, string scope = "Synthetic scope") => Json(
        "{\"expected_revision\":" + revision + ",\"content\":{\"practice\":\"advisory\",\"scope\":\"" + scope +
        "\",\"period_start\":\"2026-01-01\",\"period_end\":\"2026-12-31\",\"engagement_lead_staff_member_id\":\"" +
        lead + "\"}}");

    static StringContent Service(int epoch, string source = "Synthetic source") => Json(
        "{\"expected_service_epoch\":" + epoch + ",\"content\":{\"service_engagement_id\":\"" + Uuid.Parse(
            "4c0b3e1e-2d7a-4c4b-9d58-0e6a5a1c0001", System.Globalization.CultureInfo.InvariantCulture) +
        "\",\"service_type\":\"design\",\"started_on\":\"2026-01-01\",\"ended_on\":null,\"firm_staff_member_ids\":[\"" +
        Uuid.Parse("4c0b3e1e-2d7a-4c4b-9d58-0e6a5a1c0002", System.Globalization.CultureInfo.InvariantCulture) +
        "\"],\"involved_management_functions\":false,\"source_reference\":\"" + source + "\"}}");

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
            services.RemoveAll<IPlatformUserDirectoryReader>();
            services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = Json("{\"email_address\":\"service-engagement-transport@example.com\"}");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
