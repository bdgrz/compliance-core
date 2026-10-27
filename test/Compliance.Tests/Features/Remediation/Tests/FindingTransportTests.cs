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
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

[Trait("Category", "WebIntegration")]
public sealed class FindingTransportTests
{
    const string Base = "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}";

    [Fact]
    public async Task ShouldDescribeFindingRoutesAsIdentifiedPutOperationsGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var immediate = new[]
        {
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}",
            Base + "/revisions/{command_id}",
            Base + "/acceptances/{command_id}",
            Base + "/reopenings/{command_id}",
            Base + "/corrective-actions/{action_id}/completions/{command_id}",
        };
        var accepted = new[]
        {
            Base + "/corrective-actions/{action_id}",
            Base + "/closures/{command_id}",
        };

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in immediate)
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _), route);
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _), route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        foreach (var route in accepted)
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.GetProperty("responses").TryGetProperty("202", out _), route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("200", out _), route);
        }

        foreach (var retired in new[]
                 {
                     "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings", Base + "/revisions",
                     Base + "/corrective-actions", Base + "/acceptances", Base + "/closures", Base + "/reopenings",
                 })
            Assert.False(paths.TryGetProperty(retired, out var item) && item.TryGetProperty("post", out _), retired);
    }

    [Fact]
    public async Task ShouldReplayOriginalResponseAndConflictChangedIntentGivenFindingRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var lead = factory.CreateClient();
        using var reviewer = factory.CreateClient();
        var scope = await ArrangeAsync(factory, lead, reviewer);
        var findingId = Guid.NewGuid();
        var findingPath = $"{scope.ProgramPath}/findings/{findingId}";
        var raise = scope.Raise();
        var actionId = Guid.NewGuid();
        var actionPath = $"{findingPath}/corrective-actions/{actionId}";
        var action = $$"""{"expected_finding_revision":1,"description":"Remove access.","owner_member_id":"{{scope.OwnerMember}}","due_on":"2027-01-15"}""";
        var closePath = $"{findingPath}/closures/{Guid.NewGuid()}";
        const string close = """{"expected_revision":2,"verification_rationale":"Verified.","resolution_evidence":[{"kind":"external","reference":"https://tickets.example/1"}],"rationale":"Closed."}""";

        // Act
        using var raised = await lead.PutAsync(findingPath, Json(raise));
        var raisedText = await raised.Content.ReadAsStringAsync();
        using var raisedAgain = await lead.PutAsync(findingPath, Json(raise));
        using var changedRaise = await lead.PutAsync(findingPath, Json(raise.Replace("Stale access", "Other", StringComparison.Ordinal)));
        using var admitted = await lead.PutAsync(actionPath, Json(action));
        var admittedText = await admitted.Content.ReadAsStringAsync();
        using var admittedAgain = await lead.PutAsync(actionPath, Json(action));
        using var changedAction = await lead.PutAsync(actionPath,
            Json(action.Replace("Remove access.", "Other.", StringComparison.Ordinal)));
        using var closeBeforeWork = await reviewer.PutAsync(closePath, Json(close));
        using var read = await lead.GetAsync(findingPath);
        using var list = await lead.GetAsync($"{scope.ProgramPath}/findings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        Assert.Equal(raisedText, await raisedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedRaise.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        Assert.Equal(admittedText, await admittedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted, admittedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changedAction.StatusCode);
        Assert.Equal(actionId.ToString(), JsonDocument.Parse(admittedText).RootElement.GetProperty("operation_id").GetString());
        // The admitted action is open until completed, so the closure is accepted and then waits on the action.
        Assert.Equal(HttpStatusCode.Accepted, closeBeforeWork.StatusCode);
        using var view = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Single(view.RootElement.GetProperty("corrective_actions").EnumerateArray());
        Assert.Equal(0, view.RootElement.GetProperty("corrective_actions")[0].GetProperty("revision").GetInt32());
        Assert.Equal(1, JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
    }

    static async Task<Scope> ArrangeAsync(WebApplicationFactory<Program> factory, HttpClient lead, HttpClient reviewer)
    {
        var leadUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(lead,
            $"finding-lead-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var reviewerUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(reviewer,
            $"finding-reviewer-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var (tenant, program) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var owner = RbacIds.Member(tenant, leadUser);
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, leadUser), member => member.Register());
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, reviewerUser), member => member.Register());
        return new Scope($"/api/v1/tenants/{tenant}/programs/{program}", owner);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "finding-tests");
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

    sealed record Scope(string ProgramPath, Uuid OwnerMember)
    {
        public string Raise() => JsonSerializer.Serialize(new
        {
            source = new { kind = "manual", source_text = "Terminated users kept access." },
            title = "Stale access",
            description = "Two leavers retained access.",
            severity = "high",
            affected_scope = "VPN",
            owner_member_id = OwnerMember.ToString(),
            due_on = "2027-01-15",
            links = Array.Empty<object>(),
        });
    }
}
