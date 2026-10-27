using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "WebIntegration")]
public sealed class ProfessionalDutyTransportTests
{
    const string Duty = FirmProfessionalDuty.RuleRatifier;

    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenRecordRoute()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await LoginAndSeedAsync(factory, client);
        await FirmStaffFacts.RegisterAsync(factory.Services, staff, user);
        var assertion = Active(designation, user);
        var path = RecordPath(assertion);

        // Act
        using var first = await client.PutAsync(path, Body(assertion, 0));
        var firstText = await first.Content.ReadAsStringAsync();
        using var retried = await client.PutAsync(path, Body(assertion, 0));
        var retryText = await retried.Content.ReadAsStringAsync();
        using var changed = await client.PutAsync(path, Body(assertion, 1));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, retryText);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var document = JsonDocument.Parse(firstText);
        var operation = assertion.Evidence.AssertionId.ToString();
        Assert.Equal(operation, document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/platform/operations/{operation}", document.RootElement.GetProperty("status_url").GetString());
        Assert.Equal($"/api/v1/platform/operations/{operation}", first.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ShouldAcknowledgeWith202AndReplayExactBodyGivenRevokeRouteAfterLaterChange()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await LoginAndSeedAsync(factory, client);
        await FirmStaffFacts.RegisterAsync(factory.Services, staff, user);
        var active = Active(designation, user);
        using var recorded = await client.PutAsync(RecordPath(active), Body(active, 0));
        Assert.Equal(HttpStatusCode.Accepted, recorded.StatusCode);
        await ProfessionalDutyFacts.RunProcessAsync(factory.Services, user, Duty, null, active.Evidence.AssertionId);
        var revocation = ProfessionalDutyAssertionTestIssuer.Issue(designation, user, Duty, null, 2, "revoked",
            active.Evidence.AssertionId, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));
        var path = $"/api/v1/platform/firm-professional-duties/{designation}/assertion-revocations/{revocation.Evidence.AssertionId}";

        // Act
        using var first = await client.PutAsync(path, Body(revocation, 1));
        var firstText = await first.Content.ReadAsStringAsync();
        await ProfessionalDutyFacts.RunProcessAsync(factory.Services, user, Duty, null, revocation.Evidence.AssertionId);
        using var retried = await client.PutAsync(path, Body(revocation, 1));
        using var stale = await client.PutAsync(path, Body(revocation, 2));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.Equal(firstText, await retried.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal($"/api/v1/platform/operations/{revocation.Evidence.AssertionId}",
            first.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ShouldRejectGivenRouteIdentityThatDiffersFromSignedAssertionOrOldRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await LoginAndSeedAsync(factory, client);
        await FirmStaffFacts.RegisterAsync(factory.Services, staff, user);
        var assertion = Active(designation, user);

        // Act
        using var otherAssertion = await client.PutAsync(
            $"/api/v1/platform/firm-professional-duties/{designation}/assertions/{Uuid.CreateVersion4()}",
            Body(assertion, 0));
        using var otherDesignation = await client.PutAsync(
            $"/api/v1/platform/firm-professional-duties/{Uuid.CreateVersion4()}/assertions/{assertion.Evidence.AssertionId}",
            Body(assertion, 0));
        using var malformed = await client.PutAsync(
            $"/api/v1/platform/firm-professional-duties/{designation}/assertions/malformed", Body(assertion, 0));
        using var oldRecord = await client.PutAsync($"/api/v1/platform/firm-professional-duties/{designation}",
            Body(assertion, 0));
        using var oldRevoke = await client.PostAsync(
            $"/api/v1/platform/firm-professional-duties/{designation}/revocation", Body(assertion, 0));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, otherAssertion.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherDesignation.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldRecord.StatusCode);
        Assert.True(oldRevoke.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    static string RecordPath(ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion) =>
        $"/api/v1/platform/firm-professional-duties/{assertion.Evidence.DesignationId}/assertions/{assertion.Evidence.AssertionId}";

    static ProfessionalDutyAssertionTestIssuer.SignedAssertion Active(Uuid designation, Uuid user) =>
        ProfessionalDutyAssertionTestIssuer.Issue(designation, user, Duty, null, 1, "active", null,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));

    static StringContent Body(ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion, int revision) =>
        new("{\"signed_assertion\":\"" + assertion.Token + "\",\"expected_revision\":" +
            revision.ToString(CultureInfo.InvariantCulture) + "}", Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder =>
        {
            var values = new Dictionary<string, string?>();
            ProfessionalDutyAssertionTestIssuer.AddConfiguration(values);
            foreach (var (key, value) in values)
                builder.UseSetting(key, value);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlatformUserDirectoryReader>();
                services.AddSingleton<IPlatformUserDirectoryReader, ExistingUsers>();
            });
        });

    static async Task LoginAndSeedAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var actor = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "professional-duty-transport@example.com"),
            CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new SeedPlatformOperatorRoster(PlatformOperatorRoster.CreationId, [actor]), RequestActor.System)).IsSuccess);
    }

    sealed class ExistingUsers : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
