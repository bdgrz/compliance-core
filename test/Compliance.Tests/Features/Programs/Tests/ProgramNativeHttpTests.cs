using System.Net;
using System.Net.Http.Json;
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
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

[Trait("Category", "WebIntegration")]
public sealed class ProgramNativeHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginalCreationAndUnitBytesGivenLaterRevisionAndCurrentAuthorization()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        var request = new { name = "SOC 2", plan = Plan() };
        var path = fixture.Path + $"?tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}";
        using var first = await fixture.Client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        var command = Uuid.CreateVersion4();
        var revisionPath = fixture.Path + $"/revisions/{command}";
        using var revision = await fixture.Client.PutAsJsonAsync(revisionPath, new { expected_revision = 1, name = "Later", plan = Plan() });
        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);

        // Act
        using var retry = await fixture.Client.PutAsJsonAsync(fixture.Path, request);
        using var revisionRetry = await fixture.Client.PutAsJsonAsync(revisionPath, new { expected_revision = 1, name = "Later", plan = Plan() });
        fixture.Permissions.Allowed = false;
        using var denied = await fixture.Client.PutAsJsonAsync(fixture.Path, request);
        var history = await fixture.HistoryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("application/json", retry.Content.Headers.ContentType?.MediaType);
        Assert.Equal(original, await retry.Content.ReadAsStringAsync());
        using var registration = JsonDocument.Parse(original);
        Assert.Equal(fixture.Program.ToString(), registration.RootElement.GetProperty("program_id").GetString());
        Assert.Equal("{}", await revision.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, revisionRetry.StatusCode);
        Assert.Equal(await revision.Content.ReadAsStringAsync(), await revisionRetry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(2, history.Facts.Count);
        Assert.All(history.Facts.OfType<IProgramFact>(), fact =>
        {
            Assert.Equal(fixture.Tenant, fact.TenantId);
            Assert.Equal(fixture.Program, fact.ProgramId);
        });
    }

    [Fact]
    public async Task ShouldPersistNoChangeReceiptGivenIdentifiedCriteriaSelectionAndExactRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        using var creation = await fixture.Client.PutAsJsonAsync(fixture.Path, new { name = "SOC 2", plan = Plan() });
        Assert.Equal(HttpStatusCode.OK, creation.StatusCode);
        var edition = CriteriaCatalog.Platform.Edition.EditionId;
        var firstPath = fixture.Path + $"/criteria-edition/selections/{Uuid.CreateVersion4()}";
        var secondPath = fixture.Path + $"/criteria-edition/selections/{Uuid.CreateVersion4()}";
        var body = new { expected_revision = 1, edition_id = edition };
        using var first = await fixture.Client.PutAsJsonAsync(firstPath, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Act
        using var noChange = await fixture.Client.PutAsJsonAsync(secondPath, body);
        using var retry = await fixture.Client.PutAsJsonAsync(secondPath, body);
        var history = await fixture.HistoryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, noChange.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("{}", await noChange.Content.ReadAsStringAsync());
        Assert.Equal(await noChange.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.Equal(2, history.Owner.Revision);
        Assert.Equal(3UL, history.Owner.NativeSequence);
        Assert.Equal(0, Assert.IsType<ProgramCriteriaEditionSelected>(history.Facts[2]).ChangeCount);
        Assert.Equal(1, Assert.IsType<ProgramCriteriaEditionSelected>(history.Facts[1]).ChangeCount);
    }

    [Fact]
    public async Task ShouldRejectChangedAuthoredIntentGivenExistingProgramIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        using var creation = await fixture.Client.PutAsJsonAsync(fixture.Path, new { name = "SOC 2", plan = Plan() });
        Assert.Equal(HttpStatusCode.OK, creation.StatusCode);

        // Act
        using var changed = await fixture.Client.PutAsJsonAsync(fixture.Path, new { name = "Different", plan = Plan() });
        var history = await fixture.HistoryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Single(history.Facts);
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("invalid-id")]
    [InlineData("invalid-body")]
    public async Task ShouldRejectInvalidRequiredIdentityOrBodyGivenProgramWriteRoute(string invalid)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        var path = invalid switch
        {
            "empty-id" => $"/api/v1/tenants/{fixture.Tenant}/programs/{Uuid.Empty}",
            "invalid-id" => fixture.Path + "/revisions/not-a-uuid",
            _ => fixture.Path,
        };
        using var body = new StringContent(invalid == "invalid-body" ? "{" : "{\"name\":\"SOC 2\",\"plan\":{}}", Encoding.UTF8, "application/json");

        // Act
        using var response = await fixture.Client.PutAsync(path, body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await fixture.HistoryAsync()).Facts);
    }

    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    sealed class Fixture : IAsyncDisposable
    {
        readonly WebApplicationFactory<global::Program> _factory;
        readonly InMemoryEventStore _events = new();
        public HttpClient Client { get; }
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public string Path => $"/api/v1/tenants/{Tenant}/programs/{Program}";
        public MutablePermissions Permissions { get; } = new();

        public Fixture()
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "program-native-http-tests");
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
                    services.AddSingleton<IPermissionAuthorizer>(Permissions);
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            Client = _factory.CreateClient();
        }

        public async Task LoginAsync()
        {
            using var login = await Client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = "program-native@example.test" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        public async Task<ProgramHistorySnapshot> HistoryAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ProgramReader>().HistoryAsync(Tenant, Program);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
