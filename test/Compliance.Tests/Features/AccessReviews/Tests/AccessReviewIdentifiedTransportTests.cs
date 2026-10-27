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
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
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
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
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
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     The population, classification, expectation and exception routes are identified 200 PUT operations: the exact
///     body replays the original response and a changed body under the same identity conflicts.
/// </summary>
[Trait("Category", "WebIntegration")]
public sealed class AccessReviewIdentifiedTransportTests
{
    const string Tenants = "/api/v1/tenants/{tenant_id}";

    [Fact]
    public async Task ShouldDescribeAccessReviewOwnerRoutesAsIdentifiedPutOperationsGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var routes = new[]
        {
            Tenants + "/access-populations/{population_id}",
            Tenants + "/access-populations/record-access-population-facts/{command_id}",
            Tenants + "/access-populations/{population_id}/acceptance/{command_id}",
            Tenants + "/access-populations/{population_id}/classifications/{command_id}",
            Tenants + "/applications/{application_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}",
            Tenants + "/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval/{command_id}",
            Tenants + "/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions/{command_id}",
            Tenants + "/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions/{exception_id}",
        };

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in routes)
        {
            var operation = paths.GetProperty(route).GetProperty("put");
            Assert.True(operation.TryGetProperty("requestBody", out _), route);
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _), route);
            Assert.False(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        foreach (var retired in new[]
                 {
                     Tenants + "/access-populations",
                     Tenants + "/access-populations/{population_id}/acceptance",
                     Tenants + "/access-populations/{population_id}/classifications",
                     Tenants + "/applications/{application_id}/system-instances/{system_instance_id}/access-expectations",
                     Tenants + "/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions",
                 })
            Assert.False(paths.TryGetProperty(retired, out var item) && item.TryGetProperty("post", out _), retired);
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenPopulationRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var manager = factory.CreateClient();
        var arranged = await ArrangeAsync(factory, manager);
        var population = Guid.NewGuid();
        var open = $$"""{"application_id":"{{arranged.ApplicationId}}","system_instance_id":"{{arranged.InstanceId}}","expected_system_instance_revision":1,"observed_at":"{{DateTimeOffset.UtcNow.AddDays(-1):O}}","source":"Provider export"}""";
        var openPath = $"{arranged.Base}/access-populations/{population}";
        var recordPath = $"{arranged.Base}/access-populations/record-access-population-facts/{Guid.NewGuid()}";
        var record = $$"""{"population_id":"{{population}}","expected_revision":1,"principals":[{"provider_subject_id":"ada","principal_kind":"user_account","display_name":"Ada","status":"active"}],"entitlements":[{"provider_entitlement_id":"read","entitlement_kind":"permission","display_name":"Read"}],"group_members":[],"assignments":[{"principal_provider_subject_id":"ada","provider_entitlement_id":"read"}]}""";
        var acceptPath = $"{arranged.Base}/access-populations/{population}/acceptance/{Guid.NewGuid()}";
        const string accept = """{"expected_revision":2,"attestation":"Observed population attestation."}""";
        var classifyPath = $"{arranged.Base}/access-populations/{population}/classifications/{Guid.NewGuid()}";
        const string classify = """{"provider_subject_id":"ada","expected_revision":0,"classification":"unclassified","rationale":"Reviewed."}""";

        // Act
        using var opened = await manager.PutAsync(openPath, Json(open));
        var openedText = await opened.Content.ReadAsStringAsync();
        using var openedAgain = await manager.PutAsync(openPath, Json(open));
        using var changedOpen = await manager.PutAsync(openPath, Json(open.Replace("Provider export", "Another export",
            StringComparison.Ordinal)));
        using var recorded = await manager.PutAsync(recordPath, Json(record));
        var recordedText = await recorded.Content.ReadAsStringAsync();
        using var recordedAgain = await manager.PutAsync(recordPath, Json(record));
        using var changedRecord = await manager.PutAsync(recordPath, Json(record.Replace("\"ada\"", "\"bob\"",
            StringComparison.Ordinal)));
        using var accepted = await manager.PutAsync(acceptPath, Json(accept));
        var acceptedText = await accepted.Content.ReadAsStringAsync();
        using var acceptedAgain = await manager.PutAsync(acceptPath, Json(accept));
        using var changedAccept = await manager.PutAsync(acceptPath, Json(accept.Replace("Observed", "Changed",
            StringComparison.Ordinal)));
        using var classified = await manager.PutAsync(classifyPath, Json(classify));
        var classifiedText = await classified.Content.ReadAsStringAsync();
        using var classifiedAgain = await manager.PutAsync(classifyPath, Json(classify));
        using var changedClassify = await manager.PutAsync(classifyPath, Json(classify.Replace("Reviewed.", "Changed.",
            StringComparison.Ordinal)));

        // Assert
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(openedText, await openedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedOpen.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(recordedText, await recordedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedRecord.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(acceptedText, await acceptedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedAccept.StatusCode);
        Assert.Equal(HttpStatusCode.OK, classified.StatusCode);
        Assert.Equal(classifiedText, await classifiedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedClassify.StatusCode);
        Assert.Equal(population.ToString(), JsonDocument.Parse(openedText).RootElement.GetProperty("population_id")
            .GetString());
        Assert.Equal(1, JsonDocument.Parse(classifiedText).RootElement.GetProperty("sequence").GetInt32());
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenExpectationAndExceptionRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var manager = factory.CreateClient();
        using var approver = factory.CreateClient();
        var arranged = await ArrangeAsync(factory, manager, approver);
        var expectation = Guid.NewGuid();
        var proposalPath = $"{arranged.Base}/applications/{arranged.ApplicationId}/system-instances/{arranged.InstanceId}/access-expectations/{expectation}";
        var proposal = $$"""{"expected_revision":0,"rule_kind":"forbidden_principal_kind","parameters":{"principal_kind":"access_token"},"rationale":"No tokens.","effective_from":"{{DateTimeOffset.UtcNow.AddDays(-5):O}}"}""";
        var instancePath = $"{arranged.Base}/system-instances/{arranged.InstanceId}/access-expectations/{expectation}";
        var approvalPath = $"{instancePath}/approval/{Guid.NewGuid()}";
        const string approval = """{"expected_revision":1}""";
        var exceptionPath = $"{instancePath}/exceptions/{Guid.NewGuid()}";
        var exception = $$"""{"expected_revision":2,"provider_subject_id":"ada","rationale":"Migration.","expires_at":"{{DateTimeOffset.UtcNow.AddDays(10):O}}"}""";
        var populationExceptionPath = $"{arranged.Base}/applications/{arranged.ApplicationId}/system-instances/{arranged.InstanceId}/access-population-exceptions/{Guid.NewGuid()}";
        var populationException = $$"""{"expected_revision":0,"reason":"Export unavailable.","expires_at":"{{DateTimeOffset.UtcNow.AddDays(10):O}}"}""";

        // Act
        using var proposed = await manager.PutAsync(proposalPath, Json(proposal));
        var proposedText = await proposed.Content.ReadAsStringAsync();
        using var changedProposal = await manager.PutAsync(proposalPath, Json(proposal.Replace("No tokens.", "Other.",
            StringComparison.Ordinal)));
        using var approved = await approver.PutAsync(approvalPath, Json(approval));
        var approvedText = await approved.Content.ReadAsStringAsync();
        using var approvedAgain = await approver.PutAsync(approvalPath, Json(approval));
        using var changedApproval = await approver.PutAsync(approvalPath, Json("""{"expected_revision":2}"""));
        using var excepted = await approver.PutAsync(exceptionPath, Json(exception));
        var exceptedText = await excepted.Content.ReadAsStringAsync();
        using var exceptedAgain = await approver.PutAsync(exceptionPath, Json(exception));
        using var changedException = await approver.PutAsync(exceptionPath, Json(exception.Replace("Migration.",
            "Other.", StringComparison.Ordinal)));
        using var proposedAgain = await manager.PutAsync(proposalPath, Json(proposal));
        using var missing = await manager.PutAsync(populationExceptionPath, Json(populationException));
        var missingText = await missing.Content.ReadAsStringAsync();
        using var missingAgain = await manager.PutAsync(populationExceptionPath, Json(populationException));
        using var changedMissing = await manager.PutAsync(populationExceptionPath, Json(populationException.Replace(
            "Export unavailable.", "Other.", StringComparison.Ordinal)));

        // Assert
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        Assert.Equal(proposedText, await proposedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedProposal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(approvedText, await approvedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedApproval.StatusCode);
        Assert.Equal(HttpStatusCode.OK, excepted.StatusCode);
        Assert.Equal(exceptedText, await exceptedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedException.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Equal(missingText, await missingAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changedMissing.StatusCode);
        Assert.Equal("approved", JsonDocument.Parse(approvedText).RootElement.GetProperty("status").GetString());
        Assert.Equal("proposed", JsonDocument.Parse(proposedText).RootElement.GetProperty("status").GetString());
    }

    static async Task<Arranged> ArrangeAsync(WebApplicationFactory<Program> factory, HttpClient manager,
        HttpClient? approver = null)
    {
        var managerUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(manager,
            $"access-manager-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        if (approver is not null)
            await TenantInvitationE2ETests.LoginAsync(approver, $"access-approver-{Guid.NewGuid():N}@example.com");
        var (tenant, application, instance) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var managerMember = RbacIds.Member(tenant, managerUser);
        var now = DateTimeOffset.UtcNow.AddDays(-10);
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, managerUser), member => member.Register());
        await ControlOperationE2ETests.SeedAsync(factory, new DeclaredApplication(tenant, application),
            declared => Plain(declared.Declare("Payroll", "Run payroll", null, managerMember, "Manager", now)));
        await ControlOperationE2ETests.SeedAsync(factory, new DeclaredSystemInstance(tenant, instance),
            declared => Plain(declared.Declare(application, "Production", "aws_account", null, null,
                Uuid.CreateVersion4(), "Seeder", now)));
        return new Arranged($"/api/v1/tenants/{tenant}", application, instance);
    }

    static Result Plain<T>(Result<T> result) => result.IsSuccess ? Result.Success : Result.Failure(result.Error);

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "access-review-identified-tests");
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

    sealed record Arranged(string Base, Uuid ApplicationId, Uuid InstanceId);
}
