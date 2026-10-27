using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
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
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

[Trait("Category", "WebIntegration")]
public sealed class WorkAssignmentNativeHttpTests
{
    [Theory]
    [InlineData("claims")]
    [InlineData("assignments")]
    [InlineData("delegations")]
    [InlineData("escalations")]
    public async Task ShouldReturnOriginalTyped200GivenIdentifiedScopedHttpMutation(string kind)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var (item, revision) = await fixture.PrepareAsync(kind);
        var command = Uuid.CreateVersion4();
        var route = fixture.Route(item.WorkItemId, kind, command);
        var json = $$"""{"expected_revision":{{revision}},"assignee_member_id":"{{fixture.Source.BackupMemberId}}","reason":"Accountability decision."}""";

        // Act
        using var first = await fixture.PutAsync(route, json);
        using var replay = await fixture.PutAsync(route + $"?command_id={Uuid.CreateVersion4()}&tenant_id={Uuid.CreateVersion4()}", json);
        var firstText = await first.Content.ReadAsStringAsync();
        var secondText = await replay.Content.ReadAsStringAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkItemAssignment(fixture.Source.TenantId, fixture.Source.ProgramId, item.WorkItemId, command));

        // Assert
        Assert.True(first.StatusCode == HttpStatusCode.OK, firstText);
        Assert.True(replay.StatusCode == HttpStatusCode.OK, secondText);
        Assert.Equal(firstText, secondText);
        using var body = JsonDocument.Parse(firstText);
        Assert.Equal(item.WorkItemId.ToString(), body.RootElement.GetProperty("item").GetProperty("work_item_id").GetString());
        Assert.Equal(revision + 1, body.RootElement.GetProperty("history").GetArrayLength());
        Assert.Equal(command, ((IWorkAssignmentFact)owner.Acceptance.SelectedFact!).Operation.LogicalId);
        Assert.Equal(revision + 1, owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectConflictingBodyOrMissingRouteAndCurrentPermissionGivenOriginalHttpAssignment()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var (item, revision) = await fixture.PrepareAsync("assignments");
        var command = Uuid.CreateVersion4();
        var route = fixture.Route(item.WorkItemId, "assignments", command);
        var json = $$"""{"expected_revision":{{revision}},"assignee_member_id":"{{fixture.Source.BackupMemberId}}"}""";
        using var accepted = await fixture.PutAsync(route, json);
        Assert.True(accepted.StatusCode == HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());

        // Act
        using var conflict = await fixture.PutAsync(route, $$"""{"command_id":"{{Uuid.CreateVersion4()}}","expected_revision":{{revision}},"assignee_member_id":"{{fixture.Source.BackupMemberId}}"}""");
        using var missing = await fixture.PutAsync(route[..route.LastIndexOf('/')], json);
        fixture.Source.Permissions.Managers.Remove(fixture.MemberId);
        using var denied = await fixture.PutAsync(route, json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkItemAssignment(fixture.Source.TenantId, fixture.Source.ProgramId, item.WorkItemId))).Revision);
    }

    [Fact]
    public async Task ShouldReplayOriginalMemberPreferenceGivenLaterHttpPreferenceChange()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var command = Uuid.CreateVersion4();
        var route = $"/api/v1/tenants/{fixture.Source.TenantId}/work-digest-preference/changes/{command}";
        const string originalBody = "{\"email_digest_enabled\":false,\"time_zone_id\":\"America/New_York\"}";
        using var first = await fixture.PutAsync(route, originalBody);
        var original = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, original);
        using var later = await fixture.PutAsync($"/api/v1/tenants/{fixture.Source.TenantId}/work-digest-preference/changes/{Uuid.CreateVersion4()}",
            "{\"email_digest_enabled\":true,\"time_zone_id\":\"UTC\"}");
        Assert.True(later.StatusCode == HttpStatusCode.OK, await later.Content.ReadAsStringAsync());

        // Act
        using var replay = await fixture.PutAsync(route, originalBody);
        using var changed = await fixture.PutAsync(route, "{\"email_digest_enabled\":true}");
        using var missing = await fixture.PutAsync($"/api/v1/tenants/{fixture.Source.TenantId}/work-digest-preference", originalBody);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkDigestPreference(fixture.Source.TenantId, fixture.MemberId));

        // Assert
        Assert.True(replay.StatusCode == HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.True(missing.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        Assert.True(current.Read().EmailDigestEnabled);
        Assert.Equal("UTC", current.Read().TimeZoneId);
        Assert.Equal(2UL, current.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public required OperationsFixture Source { get; init; }
        public required WebApplicationFactory<global::Program> Factory { get; init; }
        public required HttpClient Client { get; init; }
        public Uuid UserId { get; private set; }
        public Uuid MemberId => RbacIds.Member(Source.TenantId, UserId);
        public static async Task<Fixture> CreateAsync()
        {
            var source = await OperationsFixture.CreateAsync();
            var factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton(source.Provider.GetRequiredService<IEventStore>());
                services.AddSingleton(source.Provider.GetRequiredService<IDomainEventReader>());
                services.RemoveAll<IImmutableContentStore>();
                services.AddSingleton(source.Content);
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(source.Permissions);
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton<IProgramResourceScopeResolver>(new Scope(source.TenantId, source.ProgramId));
                services.RemoveAll<WorkQueueReader>();
                services.AddScoped(provider => new WorkQueueReader(provider.GetRequiredService<IAggregateReader>(),
                    provider.GetRequiredService<WorkAssignmentReader>(), provider.GetRequiredService<ControlReader>(), provider.GetRequiredService<PolicyReader>(),
                    provider.GetRequiredService<OperatingAuthority>(), TimeProvider.System,
                    new WorkQueueReadConsistency(provider.GetRequiredService<IDomainEventReader>()), boundaries: source.Boundaries,
                    operatingReader: provider.GetRequiredService<ControlOperationsReader>(), findings: provider.GetRequiredService<FindingReader>(),
                    mappingReader: provider.GetRequiredService<ControlMappingReader>(), applicabilityReader: provider.GetRequiredService<ApplicabilityReader>(),
                    evaluationReader: provider.GetRequiredService<ControlEvaluationReader>(),
                    riskGovernance: provider.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
                    evidenceRequests: provider.GetRequiredService<EvidenceRequestReader>(),
                    boundarySources: provider.GetRequiredService<BoundaryReader>()));
            }));
            var fixture = new Fixture { Source = source, Factory = factory, Client = factory.CreateClient() };
            fixture.UserId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(fixture.Client, "work-native-manager@example.test"), CultureInfo.InvariantCulture);
            await ProgramManagementServices.SeedAsync(source.Provider, new Member(source.TenantId, fixture.UserId), member => member.Register());
            source.Permissions.Managers.Add(fixture.MemberId);
            await source.AddToTeamAsync(fixture.UserId);
            await source.AddToTeamAsync(source.BackupUserId);
            await source.PlanAsync(owner: new OperatingHolder("team", source.TeamId), backup: new OperatingHolder("member", source.BackupMemberId));
            return fixture;
        }
        public async Task<(WorkQueueItemView Item, long Revision)> PrepareAsync(string kind)
        {
            var item = (await Source.AsAsync(UserId, new ListWork(Source.TenantId, Source.ProgramId, "unassigned"))).Items.First(value => !value.Escalated);
            if (kind is not ("delegations" or "escalations"))
                return (item, 0);
            await Source.AsAsync(Source.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(), Source.TenantId, Source.ProgramId,
                item.WorkItemId, 0, MemberId, "Initial accountability."));
            return (item, 1);
        }
        public string Route(Uuid item, string kind, Uuid command) => $"/api/v1/tenants/{Source.TenantId}/programs/{Source.ProgramId}/work/{item}/{kind}/{command}";
        public async Task<HttpResponseMessage> PutAsync(string route, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            return await Client.PutAsync(route, body);
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            await Source.Provider.DisposeAsync();
        }
    }
    sealed class Scope(Uuid tenant, Uuid program) : IProgramResourceScopeResolver
    {
        public ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) => ValueTask.FromResult(tenantId == tenant && programId == program);
        public ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId, CancellationToken ct = default) => ValueTask.FromResult(false);
        public ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request, CancellationToken ct = default) => ValueTask.FromResult<Uuid?>(null);
    }
}
