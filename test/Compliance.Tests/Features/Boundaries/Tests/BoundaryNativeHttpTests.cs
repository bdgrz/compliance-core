using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

[Trait("Category", "WebIntegration")]
public sealed class BoundaryNativeHttpTests
{
    static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public async Task ShouldReplayOriginalUnitGivenIdentifiedHttpRevisionAfterDiscard()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        var author = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client, "boundary-native-author@example.test"), CultureInfo.InvariantCulture);
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var boundary = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(factory.Services, new ProgramRules(tenant, program), owner =>
        {
            Assert.Null(owner.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(tenant, author), "Author", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        var createPath = $"/api/v1/tenants/{tenant}/programs/{program}/boundaries/{boundary}";
        var content = new BoundaryContent("Original scope", "readiness", ["security"], []);

        // Act
        using var created = await client.PutAsJsonAsync(createPath, new { content }, WireJson);
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var createReplay = await client.PutAsJsonAsync(createPath, new { content }, WireJson);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await createReplay.Content.ReadAsStringAsync());
        using var registration = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var draft = registration.RootElement.GetProperty("draft_version_id").GetString();
        var draftPath = $"/api/v1/tenants/{tenant}/boundaries/{boundary}/drafts/{draft}";
        var revisionPath = $"{draftPath}/revisions/{Uuid.CreateVersion4()}";
        var revision = new { expected_revision = 1, content = content with { Statement = "Revised scope" } };
        using var revised = await client.PutAsJsonAsync(revisionPath, revision, WireJson);
        Assert.True(revised.StatusCode == HttpStatusCode.OK, await revised.Content.ReadAsStringAsync());
        using var discarded = await client.PutAsJsonAsync($"{draftPath}/discards/{Uuid.CreateVersion4()}",
            new { expected_revision = 2, rationale = "Superseded draft" }, WireJson);
        Assert.True(discarded.StatusCode == HttpStatusCode.OK, await discarded.Content.ReadAsStringAsync());
        var accepted = await ProgramManagementServices.HydrateAsync(factory.Services, new SystemBoundary(tenant, boundary));
        using var replay = await client.PutAsJsonAsync(revisionPath, revision, WireJson);
        var current = await ProgramManagementServices.HydrateAsync(factory.Services, new SystemBoundary(tenant, boundary));

        // Assert
        Assert.Equal(HttpStatusCode.OK, createReplay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("{}", await revised.Content.ReadAsStringAsync());
        Assert.Equal("{}", await replay.Content.ReadAsStringAsync());
        Assert.Equal(accepted.CommittedStreamPosition, current.CommittedStreamPosition);
    }
}
