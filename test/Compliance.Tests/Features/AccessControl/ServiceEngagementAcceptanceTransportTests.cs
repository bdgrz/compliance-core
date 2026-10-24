using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class ServiceEngagementAcceptanceTransportTests
{
    static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenAcceptanceRoute()
    {
        // Arrange
        await using var factory = Factory();
        var world = await WorldAsync(factory);
        var command = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/acceptance/{command}";

        // Act
        using var first = await world.Partner.PutAsync(path, Json("{\"expected_revision\":1}"));
        var firstText = await first.Content.ReadAsStringAsync();
        var process = await AcceptanceFacts.RunAsync(factory.Services, world.Tenant, world.Engagement, command);
        using var retried = await world.Partner.PutAsync(path, Json("{\"expected_revision\":1}"));
        var retryText = await retried.Content.ReadAsStringAsync();
        using var changed = await world.Partner.PutAsync(path, Json("{\"expected_revision\":2}"));
        using var oldRoute = await world.Partner.PostAsync(
            $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/acceptance", Json("{\"expected_revision\":1}"));
        using var accepted = await world.Partner.GetAsync(
            $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/acceptance");

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(command.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/tenants/{world.Tenant}/operations/{command}",
            document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal(document.RootElement.GetProperty("status_url").GetString(), first.Headers.Location?.OriginalString);
        Assert.True(document.RootElement.TryGetProperty("accepted_at", out _));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var view = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal("active", view.RootElement.GetProperty("status").GetString());
        Assert.Equal(world.PartnerUser.ToString(), view.RootElement.GetProperty("partner_user_id").GetString());
    }

    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenActualAssignmentRoute()
    {
        // Arrange
        await using var factory = Factory();
        var world = await WorldAsync(factory);
        var accept = Uuid.CreateVersion4();
        using var accepted = await world.Partner.PutAsync(
            $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/acceptance/{accept}",
            Json("{\"expected_revision\":1}"));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(ProcessState.Completed, (await AcceptanceFacts.RunAsync(factory.Services, world.Tenant,
            world.Engagement, accept)).State);
        var assignee = await FirmStaffFacts.RegisterAsync(factory.Services, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "advisory", "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Op"),
            IndependenceHost.At);
        var command = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/actual-assignments/" +
            $"{assignee.StaffMemberId}/{command}";
        var body = "{\"expected_revision\":2,\"effective_from\":\"" + Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\"}";

        // Act
        using var first = await world.Partner.PutAsync(path, Json(body));
        var firstText = await first.Content.ReadAsStringAsync();
        var process = await AcceptanceFacts.RunAsync(factory.Services, world.Tenant, world.Engagement, command);
        using var retried = await world.Partner.PutAsync(path, Json(body));
        using var changed = await world.Partner.PutAsync(path, Json(body.Replace("\"expected_revision\":2",
            "\"expected_revision\":3", StringComparison.Ordinal)));
        using var duplicate = await world.Partner.PutAsync(
            $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/actual-assignments/" +
            $"{assignee.StaffMemberId}/{Uuid.CreateVersion4()}", Json(body.Replace("\"expected_revision\":2",
                "\"expected_revision\":3", StringComparison.Ordinal)));
        using var current = await world.Partner.GetAsync(
            $"/api/v1/tenants/{world.Tenant}/service-engagements/{world.Engagement}/acceptance");

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, await retried.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(command.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal(document.RootElement.GetProperty("status_url").GetString(), first.Headers.Location?.OriginalString);
        using var view = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(2, view.RootElement.GetProperty("assignments").GetArrayLength());
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("malformed")]
    public async Task ShouldRejectGivenMissingOrMalformedRouteIdentity(string identity)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(client, "acceptance-routes@example.com");
        var path = $"/api/v1/tenants/{Uuid.CreateVersion4()}/service-engagements";
        var engagement = Uuid.CreateVersion4();

        // Act
        using var accept = await client.PutAsync($"{path}/{engagement}/acceptance/{identity}", Json("{\"expected_revision\":1}"));
        using var assign = await client.PutAsync(
            $"{path}/{engagement}/actual-assignments/{Uuid.CreateVersion4()}/{identity}", Json(Assignment()));
        using var assignStaff = await client.PutAsync(
            $"{path}/{engagement}/actual-assignments/{identity}/{Uuid.CreateVersion4()}", Json(Assignment()));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, accept.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        Assert.True(assignStaff.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            assignStaff.StatusCode.ToString());
    }

    [Fact]
    public async Task ShouldDenyBothRoutesGivenCallerWithoutCurrentPartnerDesignationAndRejectOldAssignmentRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(client, "acceptance-denied@example.com");
        var path = $"/api/v1/tenants/{Uuid.CreateVersion4()}/service-engagements/{Uuid.CreateVersion4()}";

        // Act
        using var accept = await client.PutAsync($"{path}/acceptance/{Uuid.CreateVersion4()}", Json("{\"expected_revision\":1}"));
        using var assign = await client.PutAsync(
            $"{path}/actual-assignments/{Uuid.CreateVersion4()}/{Uuid.CreateVersion4()}", Json(Assignment()));
        using var oldAssign = await client.PostAsync($"{path}/actual-assignments", Json(Assignment()));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        Assert.True(oldAssign.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    static async Task<World> WorldAsync(WebApplicationFactory<Program> factory)
    {
        var partner = factory.CreateClient();
        var client = factory.CreateClient();
        var partnerUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(partner, "acceptance-partner@example.com"),
            CultureInfo.InvariantCulture);
        await TenantInvitationE2ETests.LoginAsync(client, "acceptance-client@example.com");
        var tenant = Uuid.CreateVersion4();
        await PersonalDecisionFacts.SeedPartnerAsync(factory.Services, tenant, null, "advisory", partnerUser);
        var lead = await FirmStaffFacts.RegisterAsync(factory.Services, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "advisory", "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Op"),
            IndependenceHost.At);
        var engagement = Uuid.CreateVersion4();
        using var created = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/create-service-engagement",
            Json("{\"expected_revision\":0,\"content\":{\"practice\":\"advisory\",\"scope\":\"Synthetic scope\"," +
                "\"period_start\":\"2026-01-01\",\"period_end\":null,\"engagement_lead_staff_member_id\":\"" +
                lead.StaffMemberId + "\"}}"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var service = Uuid.CreateVersion4();
        using var recorded = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/nonattest-services/record-nonattest-service/{service}",
            Json("{\"expected_service_epoch\":0,\"content\":{\"service_engagement_id\":\"" + Uuid.CreateVersion4() +
                "\",\"service_type\":\"design\",\"started_on\":\"2026-01-01\",\"ended_on\":null," +
                "\"firm_staff_member_ids\":[\"" + Uuid.CreateVersion4() + "\"],\"involved_management_functions\":false," +
                "\"source_reference\":\"Synthetic source\"}}"));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var acknowledgement = Uuid.CreateVersion4();
        using var acknowledged = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/management-acknowledgements/{acknowledgement}",
            Json("{\"expected_engagement_revision\":1,\"complete_service_record_ids\":[\"" + service +
                "\"],\"statement\":\"I retain management responsibility.\",\"expected_service_epoch\":1}"));
        Assert.Equal(HttpStatusCode.Accepted, acknowledged.StatusCode);
        Assert.Equal(ProcessState.Completed, (await PersonalDecisionFacts.RunAsync(factory.Services, tenant, engagement,
            acknowledgement)).State);
        return new World(partner, tenant, engagement, partnerUser);
    }

    static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    static string Assignment() => "{\"expected_revision\":1,\"effective_from\":\"" +
        Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\"}";

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder =>
        {
            var values = new Dictionary<string, string?>();
            ProfessionalDutyAssertionTestIssuer.AddConfiguration(values);
            foreach (var (key, value) in values)
                builder.UseSetting(key, value);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
                services.RemoveAll<IPlatformUserDirectoryReader>();
                services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
            });
        });

    sealed record World(HttpClient Partner, Uuid Tenant, Uuid Engagement, Uuid PartnerUser);

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
