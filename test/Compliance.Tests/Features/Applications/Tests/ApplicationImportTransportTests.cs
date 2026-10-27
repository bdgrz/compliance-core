using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

[Trait("Category", "WebIntegration")]
public sealed class ApplicationImportTransportTests
{
    [Fact]
    public async Task ShouldDescribeImportRoutesAsPutWithAcceptedResponsesGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        const string imports = "/api/v1/tenants/{tenant_id}/application-imports";
        const string batch = imports + "/{batch_id}";

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        Assert.False(paths.TryGetProperty(imports, out _));
        Assert.True(paths.GetProperty(imports + "/{submission_id}").TryGetProperty("put", out var stage));
        Assert.True(stage.GetProperty("responses").TryGetProperty("200", out _));
        foreach (var route in new[] { batch + "/acceptances/{command_id}", batch + "/cancellations/{command_id}" })
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("202", out _));
            Assert.Contains("command_id", operation.GetProperty("parameters").EnumerateArray()
                .Select(parameter => parameter.GetProperty("name").GetString()));
        }

        foreach (var route in new[] { batch + "/rows/{row_id}/correlations/{command_id}",
                     batch + "/omission-proposal/{command_id}" })
            Assert.True(paths.GetProperty(route).TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty(batch + "/acceptances", out _));
        Assert.False(paths.TryGetProperty(batch + "/cancellations", out _));
        Assert.False(paths.TryGetProperty(batch + "/rows/{row_id}/correlations", out _));
        Assert.False(paths.GetProperty(batch + "/omission-proposal").TryGetProperty("post", out _));
    }

    [Fact]
    public async Task ShouldAcknowledgeWith202ReplayExactBodyAndConflictChangedIntentGivenAcceptRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, _) = await LoginAsync(client);
        var (batch, _) = await StageAndCorrelateAsync(client, tenant);
        var command = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/application-imports/{batch}/acceptances/{command}";

        // Act
        using var first = await client.PutAsync(path, Json("""{"expected_revision":2}"""));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Json("""{"expected_revision":2}"""));
        var retryText = await retried.Content.ReadAsStringAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var run = await ApplicationImportProcessSupport.RunAsync(factory.Services, scope.ServiceProvider, tenant, batch);
        using var afterPublication = await client.PutAsync(path, Json("""{"expected_revision":2}"""));
        using var changed = await client.PutAsync(path, Json("""{"expected_revision":3}"""));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, afterPublication.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(ProcessState.Completed, run.State);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(command.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/platform/operations/{command}", document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal($"/api/v1/platform/operations/{command}", first.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ShouldAcceptCancellationWith202ReplayAndConflictChangedIntentGivenCancelRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, _) = await LoginAsync(client);
        var (batch, _) = await StageAndCorrelateAsync(client, tenant);
        var path = $"/api/v1/tenants/{tenant}/application-imports/{batch}/cancellations/{Uuid.CreateVersion4()}";

        // Act
        using var canceled = await client.PutAsync(path, Json("""{"expected_revision":2,"reason":"Superseded"}"""));
        var firstText = await canceled.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Json("""{"expected_revision":2,"reason":"Superseded"}"""));
        using var changed = await client.PutAsync(path, Json("""{"expected_revision":2,"reason":"A different reason"}"""));
        await using var scope = factory.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ApplicationImportBatch(tenant, batch));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, canceled.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(await retried.Content.ReadAsStringAsync(), firstText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.NotNull(saved.CancellationAccepted);
        Assert.Equal("canceling", saved.State);
    }

    [Fact]
    public async Task ShouldReplayOriginalRegistrationAndRejectOldPostRouteGivenStagePutRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (tenant, _) = await LoginAsync(client);
        var submission = Uuid.CreateVersion4();
        var body = """{"source_key":"manual","source_namespace":"applications","coverage":"partial","rows":[{"source_record_id":"record-1","name":"Payroll","purpose":"Pay staff","owner_reference":null}]}""";
        var path = $"/api/v1/tenants/{tenant}/application-imports/{submission}";

        // Act
        using var first = await client.PutAsync(path, Json(body));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Json(body));
        using var changed = await client.PutAsync(path, Json(body.Replace("Payroll", "Changed", StringComparison.Ordinal)));
        using var oldRoute = await client.PostAsync($"/api/v1/tenants/{tenant}/application-imports",
            Json($$"""{"submission_id":"{{submission}}",{{body[1..]}}"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(firstText, await retried.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        using var document = JsonDocument.Parse(firstText);
        Assert.Equal(1, document.RootElement.GetProperty("revision").GetInt64());
    }

    static async Task<(Uuid Batch, Uuid Row)> StageAndCorrelateAsync(HttpClient client, Uuid tenant)
    {
        var submission = Uuid.CreateVersion4();
        using var staged = await client.PutAsync($"/api/v1/tenants/{tenant}/application-imports/{submission}", Json(
            """{"source_key":"manual","source_namespace":"applications","coverage":"partial","rows":[{"source_record_id":"record-1","name":"Payroll","purpose":"Pay staff","owner_reference":null}]}"""));
        Assert.Equal(HttpStatusCode.OK, staged.StatusCode);
        using var document = JsonDocument.Parse(await staged.Content.ReadAsStringAsync());
        var batch = Uuid.Parse(document.RootElement.GetProperty("batch_id").GetString()!, CultureInfo.InvariantCulture);
        var row = ApplicationImportEffects.RowId(batch, 1);
        using var correlated = await client.PutAsync(
            $"/api/v1/tenants/{tenant}/application-imports/{batch}/rows/{row}/correlations/{Uuid.CreateVersion4()}",
            Json("""{"expected_revision":1,"decision":"create_new","reason":"Reviewed identity"}"""));
        Assert.True(correlated.IsSuccessStatusCode, await correlated.Content.ReadAsStringAsync());
        return (batch, row);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static async Task<(Uuid Tenant, Uuid User)> LoginAsync(HttpClient client)
    {
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "application-import-transport@example.com"),
            CultureInfo.InvariantCulture);
        return (Uuid.CreateVersion4(), user);
    }

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "application-import-tests");
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
            });
        });
}
