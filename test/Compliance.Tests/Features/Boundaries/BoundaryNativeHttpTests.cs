using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

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
        await ProgramManagementServices.SeedAsync(factory.Services, new ComplianceProgram(tenant, program), owner =>
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
