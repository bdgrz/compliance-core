using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

[Trait("Category", "WebIntegration")]
public sealed class AccessReviewTransportTests
{
    const string Base = "/api/v1/tenants/{tenant_id}/access-review-campaigns";

    [Fact]
    public async Task ShouldDescribeCampaignAndItemRoutesAsIdentifiedPutOperationsGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var ok = new[]
        {
            Base + "/{campaign_id}",
            Base + "/{campaign_id}/items/{item_id}/decisions/{command_id}",
            Base + "/{campaign_id}/items/{item_id}/remediation-changes/{command_id}",
            Base + "/{campaign_id}/items/{item_id}/remediation-verifications/{command_id}",
            Base + "/{campaign_id}/items/{item_id}/remediation-exceptions/{command_id}",
            Base + "/{campaign_id}/items/{item_id}/responsibility-reassignments/{command_id}",
        };
        const string completion = Base + "/{campaign_id}/completion/{command_id}";

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in ok)
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _) || route.EndsWith("{campaign_id}", StringComparison.Ordinal), route);
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _), route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        var accepted = paths.GetProperty(completion).GetProperty("put");
        Assert.True(accepted.GetProperty("responses").TryGetProperty("202", out _));
        Assert.Contains("command_id", accepted.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()));
        foreach (var retired in new[]
                 {
                     Base, Base + "/{campaign_id}/items/{item_id}/decisions", Base + "/{campaign_id}/completion",
                     Base + "/{campaign_id}/items/{item_id}/remediation-changes", Base + "/{campaign_id}/bulk-decisions",
                 })
            Assert.False(paths.TryGetProperty(retired, out var item) && item.TryGetProperty("post", out _), retired);
        var bulk = paths.GetProperty(Base + "/{campaign_id}/bulk-decisions/{decision_batch_id}").GetProperty("put");
        Assert.True(bulk.GetProperty("responses").TryGetProperty("202", out _));
    }

    [Fact]
    public async Task ShouldRouteLaunchPutToTheHandlerAndRejectTheOldPostRouteGivenUnknownProgram()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var tenant = await LoginAsync(client);
        var campaign = Uuid.CreateVersion4();
        var body = $$"""{"name":"Q3","instructions":"Review.","deadline":"2099-01-01T00:00:00Z","assignments":[{"population_id":"{{Uuid.CreateVersion4()}}","reviewer_member_id":"{{Uuid.CreateVersion4()}}"}],"program_id":"{{Uuid.CreateVersion4()}}","remediation_owner_member_id":"{{Uuid.CreateVersion4()}}"}""";

        // Act
        using var launched = await client.PutAsync($"/api/v1/tenants/{tenant}/access-review-campaigns/{campaign}", Json(body));
        using var oldRoute = await client.PostAsync($"/api/v1/tenants/{tenant}/access-review-campaigns", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, launched.StatusCode);
        Assert.Contains("owner Program", await launched.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ShouldRouteCompletionAndItemPutsToTheHandlersAndRejectOldRoutesGivenUnknownCampaign()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var tenant = await LoginAsync(client);
        var (campaign, item) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var basePath = $"/api/v1/tenants/{tenant}/access-review-campaigns/{campaign}";

        // Act
        using var completion = await client.PutAsync($"{basePath}/completion/{Uuid.CreateVersion4()}",
            Json("""{"expected_revision":1,"attestation":"Reviewed."}"""));
        using var decision = await client.PutAsync($"{basePath}/items/{item}/decisions/{Uuid.CreateVersion4()}",
            Json("""{"expected_revision":1,"decision":"keep","rationale":"Needed."}"""));
        using var change = await client.PutAsync($"{basePath}/items/{item}/remediation-changes/{Uuid.CreateVersion4()}",
            Json("""{"expected_revision":1,"reference":"T-1","description":"Done.","changed_at":"2026-01-01T00:00:00Z"}"""));
        using var oldCompletion = await client.PostAsync($"{basePath}/completion",
            Json("""{"expected_revision":1,"attestation":"Reviewed."}"""));
        using var oldDecision = await client.PostAsync($"{basePath}/items/{item}/decisions",
            Json("""{"expected_revision":1,"decision":"keep","rationale":"Needed."}"""));
        using var bulk = await client.PostAsync($"{basePath}/bulk-decisions",
            Json("""{"item_ids":[],"decision":"keep","rationale":"Needed.","preview_token":"x"}"""));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, completion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, decision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.True(oldCompletion.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.True(oldDecision.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.True(bulk.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static async Task<Uuid> LoginAsync(HttpClient client)
    {
        _ = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "access-review-transport@example.com"),
            CultureInfo.InvariantCulture);
        return Uuid.CreateVersion4();
    }

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "access-review-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor =>
                             descriptor.ServiceType == typeof(IHostedService) &&
                             descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                    services.Remove(descriptor);
                var store = new InMemoryEventStore();
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.RemoveAll<IKvClient>();
                services.AddSingleton<IKvClient>(new InMemoryKvClient());
                services.RemoveAll<ITenantMembershipDirectoryReader>();
                services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                services.RemoveAll<ITenantActivity>();
                services.AddSingleton<ITenantActivity, ActiveTenant>();
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                    new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton(ProgramManagementServices.ResourceScopes());
            });
        });
}
