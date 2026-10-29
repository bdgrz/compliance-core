using System.Net;
using System.Text;
using System.Text.Json;
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
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

/// <summary>
///     The identified inventory routes through the production host: declarations and system instances are created
///     by PUT under their own identity, revisions and retirements by PUT under their command, and an exact retry
///     replays the original response.
/// </summary>
[Trait("Category", "WebIntegration")]
public sealed class ApplicationInventoryHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginalRegistrationGivenExactPutRetryAndConflictGivenChangedBody()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var applicationId = Uuid.CreateVersion4();

        // Act
        using var created = await fixture.PutAsync($"applications/{applicationId}", fixture.DeclareBody("Payroll"));
        using var retry = await fixture.PutAsync($"applications/{applicationId}", fixture.DeclareBody("Payroll"));
        using var changed = await fixture.PutAsync($"applications/{applicationId}", fixture.DeclareBody("Other"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(applicationId, await RegisteredIdAsync(created, "application_id"));
        Assert.Equal(applicationId, await RegisteredIdAsync(retry, "application_id"));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Single(await fixture.EventsAsync("applications", applicationId));
    }

    [Theory]
    [InlineData("applications/not-a-uuid")]
    [InlineData("applications/00000000-0000-0000-0000-000000000000")]
    public async Task ShouldRejectWithoutAppendGivenMalformedOrEmptyRouteIdentity(string path)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        // Act
        using var response = await fixture.PutAsync(path, fixture.DeclareBody("Payroll"));

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, response.StatusCode.ToString());
        Assert.Empty(await fixture.AllApplicationEventsAsync());
    }

    [Fact]
    public async Task ShouldNoLongerCreateApplicationsGivenPostToCollectionRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        // Act
        using var response = await fixture.PostAsync("applications", fixture.DeclareBody("Payroll"));

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            response.StatusCode.ToString());
        Assert.Empty(await fixture.AllApplicationEventsAsync());
    }

    [Fact]
    public async Task ShouldReviseAndRetireUnderCommandIdsGivenCommandRoutesAndReplayExactRetries()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var applicationId = Uuid.CreateVersion4();
        using var created = await fixture.PutAsync($"applications/{applicationId}", fixture.DeclareBody("Payroll"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var revise = Uuid.CreateVersion4();
        var retire = Uuid.CreateVersion4();
        var reviseBody = $"{{\"application_id\":\"{applicationId}\",\"expected_revision\":1,\"name\":\"Payroll v2\",\"purpose\":\"Run payroll\",\"owner_reference\":\"Finance\"}}";
        var retireBody = "{\"expected_revision\":2,\"effective_at\":\"2026-10-24T12:00:00Z\",\"reason\":\"Decommissioned\"}";

        // Act
        using var revised = await fixture.PutAsync($"applications/revise-application/{revise}", reviseBody);
        using var revisedAgain = await fixture.PutAsync($"applications/revise-application/{revise}", reviseBody);
        using var staleOther = await fixture.PutAsync($"applications/revise-application/{Uuid.CreateVersion4()}", reviseBody);
        using var retired = await fixture.PutAsync($"applications/{applicationId}/retirements/{retire}", retireBody);
        using var retiredAgain = await fixture.PutAsync($"applications/{applicationId}/retirements/{retire}", retireBody);
        using var retiredChanged = await fixture.PutAsync($"applications/{applicationId}/retirements/{retire}",
            retireBody.Replace("Decommissioned", "Another reason", StringComparison.Ordinal));

        // Assert
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revisedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retiredAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, retiredChanged.StatusCode);
        var events = await fixture.EventsAsync("applications", applicationId);
        Assert.Collection(events, ev => Assert.IsType<ApplicationDeclared>(ev.Event),
            ev => Assert.Equal(revise, Assert.IsType<ApplicationRevised>(ev.Event).Operation.LogicalId),
            ev => Assert.Equal(retire, Assert.IsType<ApplicationRetired>(ev.Event).Operation.LogicalId));
    }

    [Fact]
    public async Task ShouldDeclareAndRetireSystemInstancesUnderTheirIdentitiesGivenPutRoutes()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var applicationId = Uuid.CreateVersion4();
        using var created = await fixture.PutAsync($"applications/{applicationId}", fixture.DeclareBody("Payroll"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var instanceId = Uuid.CreateVersion4();
        var retire = Uuid.CreateVersion4();
        var declareBody = "{\"expected_application_revision\":1,\"name\":\"Production\",\"kind\":\"production\",\"source_identifier\":\"payroll-prod\"}";
        var retireBody = "{\"expected_revision\":1,\"effective_at\":\"2026-10-24T12:00:00Z\",\"reason\":\"Decommissioned\"}";
        var instancePath = $"applications/{applicationId}/system-instances/{instanceId}";

        // Act
        using var declared = await fixture.PutAsync(instancePath, declareBody);
        using var declaredAgain = await fixture.PutAsync(instancePath, declareBody);
        using var retired = await fixture.PutAsync($"{instancePath}/retirements/{retire}", retireBody);
        using var retiredAgain = await fixture.PutAsync($"{instancePath}/retirements/{retire}", retireBody);
        using var declaredAfterRetirement = await fixture.PutAsync(instancePath, declareBody);

        // Assert
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        Assert.Equal(instanceId, await RegisteredIdAsync(declared, "system_instance_id"));
        Assert.Equal(HttpStatusCode.OK, declaredAgain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retiredAgain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, declaredAfterRetirement.StatusCode);
        Assert.Collection(await fixture.EventsAsync("system-instances", instanceId),
            ev => Assert.IsType<SystemInstanceRegistered>(ev.Event),
            ev => Assert.IsType<SystemInstanceRetired>(ev.Event));
    }

    static async Task<Uuid> RegisteredIdAsync(HttpResponseMessage response, string property)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Uuid.Parse(json.RootElement.GetProperty(property).GetString()!, null);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly WebApplicationFactory<Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();

        public Fixture()
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "application-inventory-http-tests");
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                        services.Remove(descriptor);
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IDomainEventReader>();
                    services.AddSingleton<IEventStore>(_events);
                    services.AddSingleton<IDomainEventReader>(_events);
                    services.RemoveAll<IKvClient>();
                    services.AddSingleton<IKvClient>(new InMemoryKvClient());
                    services.RemoveAll<ITenantMembershipDirectoryReader>();
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                    services.RemoveAll<ITenantActivity>();
                    services.AddSingleton<ITenantActivity, ActiveTenant>();
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
                });
            });
            _client = _factory.CreateClient();
        }

        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();

        public async Task SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"inventory-author@example.com\"}", Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var owner = new PersonRules(Tenant, Owner);
            Assert.True(owner.Record("Owner", null, ActorReference.ForMember(Uuid.CreateVersion4(), "Author"),
                ApplicationInventoryHost.Now).IsSuccess);
            await PersonSeeds.AppendAsync(scope.ServiceProvider, owner);
        }

        public string DeclareBody(string name) =>
            $"{{\"name\":\"{name}\",\"purpose\":\"Run payroll\",\"owner_reference\":\"Finance\",\"classification\":\"internal\",\"system_owner_person_id\":\"{Owner}\",\"access_owner_person_id\":\"{Owner}\",\"application_kind\":\"service\",\"criticality\":\"critical\"}}";

        public Task<HttpResponseMessage> PutAsync(string path, string json) =>
            _client.PutAsync($"/api/v1/tenants/{Tenant}/{path}", new StringContent(json, Encoding.UTF8, "application/json"));

        public Task<HttpResponseMessage> PostAsync(string path, string json) =>
            _client.PostAsync($"/api/v1/tenants/{Tenant}/{path}", new StringContent(json, Encoding.UTF8, "application/json"));

        public async Task<List<DomainEventRecord>> EventsAsync(string area, Uuid resource)
        {
            var records = new List<DomainEventRecord>();
            try
            {
                await foreach (var record in _events.ReadAsync(new EventStreamAddress(Tenant.ToString(), area,
                                   resource.ToString()), 0, CancellationToken.None))
                    records.Add(record);
            }
            catch (InvalidOperationException)
            {
                // The in-memory store reports an unwritten stream this way.
            }

            return records;
        }

        public async Task<List<DomainEventRecord>> AllApplicationEventsAsync()
        {
            var records = new List<DomainEventRecord>();
            try
            {
                await foreach (var record in ((IDomainEventReader)_events).ReadAsync(
                                   EventStreamPattern.ForPattern(Tenant.ToString(), "applications"),
                                   ProjectionCheckpoint.Start.Cursor, CancellationToken.None))
                    records.Add(record);
            }
            catch (InvalidOperationException)
            {
                // The in-memory store reports an unwritten pattern this way.
            }

            return records;
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }
}
