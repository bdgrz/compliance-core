using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "WebIntegration")]
public sealed class EngagementPersonalDecisionTransportTests
{
    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenManagementAcknowledgementRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var (tenant, engagement, service) = await SeedAsync(factory, client);
        var acknowledgement = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/management-acknowledgements/{acknowledgement}";
        var body = Acknowledge(1, service, 1);

        // Act
        using var first = await client.PutAsync(path, Json(body));
        var firstText = await first.Content.ReadAsStringAsync();
        using var later = await client.PutAsync($"/api/v1/tenants/{tenant}/nonattest-services/record-nonattest-service/{Uuid.CreateVersion4()}",
            Service(1));
        using var retried = await client.PutAsync(path, Json(body));
        var retryText = await retried.Content.ReadAsStringAsync();
        using var changed = await client.PutAsync(path, Json(Acknowledge(1, service, 1, "A different statement.")));
        using var stale = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/management-acknowledgements/{Uuid.CreateVersion4()}",
            Json(body));
        using var oldRoute = await client.PostAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/management-acknowledgements/{acknowledgement}",
            Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(acknowledgement.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/tenants/{tenant}/operations/{acknowledgement}",
            document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal(document.RootElement.GetProperty("status_url").GetString(), first.Headers.Location?.OriginalString);
        Assert.True(document.RootElement.TryGetProperty("accepted_at", out _));
    }

    [Fact]
    public async Task ShouldListMaterializedAcknowledgementGivenProcessDrivenAdmission()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var (tenant, engagement, service) = await SeedAsync(factory, client);
        var acknowledgement = Uuid.CreateVersion4();
        var basePath = $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/management-acknowledgements";
        using var admitted = await client.PutAsync($"{basePath}/{acknowledgement}", Json(Acknowledge(1, service, 1)));
        using var before = await client.GetAsync(basePath);

        // Act
        var process = await PersonalDecisionFacts.RunAsync(factory.Services, tenant, engagement, acknowledgement);
        using var after = await client.GetAsync(basePath);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var empty = JsonDocument.Parse(await before.Content.ReadAsStringAsync());
        Assert.Equal(0, empty.RootElement.GetArrayLength());
        Assert.Equal(ProcessState.Completed, process.State);
        using var listed = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        var item = Assert.Single(listed.RootElement.EnumerateArray());
        Assert.Equal(acknowledgement.ToString(), item.GetProperty("acknowledgement_id").GetString());
        Assert.Equal(service.ToString(), Assert.Single(item.GetProperty("complete_service_record_ids").EnumerateArray())
            .GetString());
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
        var path = $"/api/v1/tenants/{tenant}/service-engagements";

        // Act
        using var acknowledge = await client.PutAsync($"{path}/{engagement}/management-acknowledgements/{identity}",
            Json(Acknowledge(1, Uuid.CreateVersion4(), 1)));
        using var acknowledgeEngagement = await client.PutAsync(
            $"{path}/{identity}/management-acknowledgements/{Uuid.CreateVersion4()}",
            Json(Acknowledge(1, Uuid.CreateVersion4(), 1)));
        using var partner = await client.PutAsync($"{path}/{engagement}/partner-evaluations/{identity}", Json(Partner()));
        using var partnerEngagement = await client.PutAsync(
            $"{path}/{identity}/partner-evaluations/{Uuid.CreateVersion4()}", Json(Partner()));

        // Assert
        Assert.All(new[] { acknowledge, acknowledgeEngagement, partner },
            response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));

        // The partner authorizer judges the caller before the engagement of an otherwise well-formed route.
        Assert.True(partnerEngagement.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            partnerEngagement.StatusCode.ToString());
    }

    [Fact]
    public async Task ShouldDenyPartnerEvaluationGivenCallerWithoutCurrentDesignationAndRejectOldRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/partner-evaluations";

        // Act
        using var denied = await client.PutAsync($"{path}/{Uuid.CreateVersion4()}", Json(Partner()));
        using var oldRoute = await client.PostAsync(path, Json(Partner()));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    static async Task<(Uuid Tenant, Uuid Engagement, Uuid Service)> SeedAsync(
        WebApplicationFactory<Program> factory, HttpClient client)
    {
        var tenant = Uuid.CreateVersion4();
        var lead = await FirmStaffFacts.RegisterAsync(factory.Services, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "advisory", "Synthetic staff source", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Op"),
            IndependenceHost.At);
        var engagement = Uuid.CreateVersion4();
        using var created = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/service-engagements/{engagement}/create-service-engagement", Draft(lead.StaffMemberId));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var service = Uuid.CreateVersion4();
        using var recorded = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/nonattest-services/record-nonattest-service/{service}", Service(0));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        return (tenant, engagement, service);
    }

    static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    static string Acknowledge(int revision, Uuid service, int epoch, string statement = "I retain management responsibility.") =>
        "{\"expected_engagement_revision\":" + revision + ",\"complete_service_record_ids\":[\"" + service +
        "\"],\"statement\":\"" + statement + "\",\"expected_service_epoch\":" + epoch + "}";

    static string Partner() =>
        "{\"expected_engagement_revision\":1,\"rule_version\":1,\"expected_rule_content_digest\":\"" +
        new string('a', 64) + "\",\"expected_service_history_digest\":\"" + new string('b', 64) +
        "\",\"rationale\":\"Synthetic rationale\",\"expected_service_epoch\":1}";

    static StringContent Draft(Uuid lead) => Json(
        "{\"expected_revision\":0,\"content\":{\"practice\":\"advisory\",\"scope\":\"Synthetic scope\"," +
        "\"period_start\":\"2026-01-01\",\"period_end\":\"2026-12-31\",\"engagement_lead_staff_member_id\":\"" + lead + "\"}}");

    static StringContent Service(int epoch) => Json(
        "{\"expected_service_epoch\":" + epoch + ",\"content\":{\"service_engagement_id\":\"" + Uuid.Parse(
            "4c0b3e1e-2d7a-4c4b-9d58-0e6a5a1c0001", System.Globalization.CultureInfo.InvariantCulture) +
        "\",\"service_type\":\"design\",\"started_on\":\"2026-01-01\",\"ended_on\":null,\"firm_staff_member_ids\":[\"" +
        Uuid.Parse("4c0b3e1e-2d7a-4c4b-9d58-0e6a5a1c0002", System.Globalization.CultureInfo.InvariantCulture) +
        "\"],\"involved_management_functions\":false,\"source_reference\":\"Synthetic source\"}}");

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
        using var body = Json("{\"email_address\":\"personal-decision-transport@example.com\"}");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
