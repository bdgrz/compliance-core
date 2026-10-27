using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Operations.Events;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
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

namespace Bdgrz.Compliance.Tests.Features.Operations;

[Trait("Category", "WebIntegration")]
public sealed class TypeIIRangeTransportTests
{
    [Fact]
    public async Task ShouldDescribeRangeRoutesAsPutWithAcceptedResponsesGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        const string start = "/api/v1/tenants/{tenant_id}/programs/{program_id}/type-ii-periods/{period_id}";
        const string amend = start + "/amendments/{command_id}";
        const string cancel = start + "/amendments/{amendment_id}/cancellations/{command_id}";

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in new[] { start, amend })
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("202", out _));
        }

        Assert.Contains("command_id", paths.GetProperty(amend).GetProperty("put").GetProperty("parameters")
            .EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()));
        Assert.Contains("amendment_id", paths.GetProperty(cancel).GetProperty("put").GetProperty("parameters")
            .EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()));
        Assert.False(paths.TryGetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/examinations/{examination_id}/observation-periods/{period_id}/starts",
            out _));
        Assert.False(paths.TryGetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}/amendments", out _));
    }

    [Fact]
    public async Task ShouldAcknowledgeWith202ReplayExactBodyAndConflictChangedIntentGivenAmendRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, user) = await LoginAsync(client);
        await using var fixture = new TypeIIRangeFixture(factory.Services, tenant, user);
        var period = await fixture.StartPeriodAsync();
        var command = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/programs/{fixture.ProgramId}/type-ii-periods/{period}/amendments/{command}";
        var body = Json($$"""{"expected_revision":1,"period_end":"{{fixture.FirstEnd.AddDays(7):yyyy-MM-dd}}","amendment_reason":"Extend the window."}""");

        // Act
        using var first = await client.PutAsync(path, body);
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Json($$"""{"expected_revision":1,"period_end":"{{fixture.FirstEnd.AddDays(7):yyyy-MM-dd}}","amendment_reason":"Extend the window."}"""));
        var retryText = await retried.Content.ReadAsStringAsync();
        await fixture.RunAsync(period, command);
        using var afterApply = await client.PutAsync(path, Json($$"""{"expected_revision":1,"period_end":"{{fixture.FirstEnd.AddDays(7):yyyy-MM-dd}}","amendment_reason":"Extend the window."}"""));
        using var changed = await client.PutAsync(path, Json($$"""{"expected_revision":1,"period_end":"{{fixture.FirstEnd.AddDays(8):yyyy-MM-dd}}","amendment_reason":"Extend the window."}"""));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, afterApply.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(command.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/platform/operations/{command}", document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal($"/api/v1/platform/operations/{command}", first.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ShouldCancelPendingAmendmentAndReplayGivenCancellationRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, user) = await LoginAsync(client);
        await using var fixture = new TypeIIRangeFixture(factory.Services, tenant, user);
        var period = await fixture.StartPeriodAsync();
        var amendment = Uuid.CreateVersion4();
        var basePath = $"/api/v1/tenants/{tenant}/programs/{fixture.ProgramId}/type-ii-periods/{period}";
        using var amended = await client.PutAsync($"{basePath}/amendments/{amendment}",
            Json($$"""{"expected_revision":1,"period_end":"{{fixture.FirstEnd.AddDays(7):yyyy-MM-dd}}","amendment_reason":"Extend the window."}"""));
        Assert.Equal(HttpStatusCode.Accepted, amended.StatusCode);
        var cancelPath = $"{basePath}/amendments/{amendment}/cancellations/{Uuid.CreateVersion4()}";

        // Act
        using var canceled = await client.PutAsync(cancelPath,
            Json("""{"expected_revision":1,"reason":"The window is no longer needed."}"""));
        using var retried = await client.PutAsync(cancelPath,
            Json("""{"expected_revision":1,"reason":"The window is no longer needed."}"""));
        using var changed = await client.PutAsync(cancelPath,
            Json("""{"expected_revision":1,"reason":"A different reason."}"""));
        var saved = await fixture.PeriodAsync(period);

        // Assert
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        Assert.Equal("{}", await canceled.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.IsType<TypeIIPeriodAmendmentCanceled>(saved.TerminalFor(amendment));
    }

    [Fact]
    public async Task ShouldRouteStartPutToTheHandlerAndRejectTheOldPostRouteGivenUnknownPlan()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, _) = await LoginAsync(client);
        var (program, period, examination) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var body = $$"""{"examination_id":"{{examination}}","plan_id":"{{Uuid.CreateVersion4()}}","period_start":"2026-10-01","period_end":"2027-09-30"}""";

        // Act
        using var start = await client.PutAsync($"/api/v1/tenants/{tenant}/programs/{program}/type-ii-periods/{period}",
            Json(body));
        using var oldRoute = await client.PostAsync(
            $"/api/v1/tenants/{tenant}/programs/{program}/examinations/{examination}/observation-periods/{period}/starts",
            Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Contains("operating plan", await start.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static async Task<(Uuid Tenant, Uuid User)> LoginAsync(HttpClient client)
    {
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "type-ii-range-transport@example.com"),
            CultureInfo.InvariantCulture);
        return (Uuid.CreateVersion4(), user);
    }

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "type-ii-range-tests");
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
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                    new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton(ProgramManagementServices.ResourceScopes());
            });
        });
}
