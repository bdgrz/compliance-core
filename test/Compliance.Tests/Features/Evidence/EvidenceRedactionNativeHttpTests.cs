using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using static Bdgrz.Compliance.Tests.Features.Evidence.EvidenceRedactionCompositionTests;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

[Trait("Category", "WebIntegration")]
public sealed class EvidenceRedactionNativeHttpTests
{
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task ShouldReplayOriginalPreparationBytesAndRejectPathBodyConflictGivenActualCurrentSourceAuthority()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await RegisterDeveloperIdentityAsync(fixture, fixture.UserId, "redaction-http@example.com");
        await using var factory = Factory(fixture);
        using var client = factory.CreateClient();
        await LoginAsync(client, "redaction-http@example.com", fixture.UserId);
        var id = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var root = $"/api/v1/tenants/{fixture.TenantId}/evidence-redactions/{id}";
        var path = $"{root}/preparations/{command}";

        // Act
        using var first = await client.PutAsync(path, Json(new
        {
            expected_revision = 0,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "First reason"
        }));
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        using var later = await client.PutAsync($"{root}/preparations/{Uuid.CreateVersion4()}", Json(new
        {
            expected_revision = 1,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "Later reason"
        }));
        using var retry = await client.PutAsync(path, Json(new
        {
            expected_revision = 0,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "First reason"
        }));
        using var conflict = await client.PutAsync(path, Json(new
        {
            command_id = Uuid.CreateVersion4(),
            expected_revision = 0,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "First reason"
        }));
        using var changed = await client.PutAsync(path, Json(new
        {
            expected_revision = 0,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "Changed authored intent"
        }));
        using var oldRoute = await client.PostAsync($"{root}/preparations", Json(new
        {
            expected_revision = 2,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "Old route"
        }));
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("application/json", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal(firstBytes, await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.True(oldRoute.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.Equal(2, history.Facts.Count);
        using var original = JsonDocument.Parse(firstBytes);
        Assert.Equal(1, original.RootElement.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task ShouldReplayOriginalPersonalApprovalBytesGivenLaterPreparationAndRealIndependentLeadSession()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        await RegisterDeveloperIdentityAsync(fixture, fixture.UserId, "redaction-preparer@example.com");
        await RegisterDeveloperIdentityAsync(fixture, lead, "redaction-lead@example.com");
        await using var factory = Factory(fixture);
        using var preparer = factory.CreateClient();
        using var approver = factory.CreateClient();
        await LoginAsync(preparer, "redaction-preparer@example.com", fixture.UserId);
        await LoginAsync(approver, "redaction-lead@example.com", lead);
        var id = Uuid.CreateVersion4();
        var preparation = Uuid.CreateVersion4();
        var approval = Uuid.CreateVersion4();
        var root = $"/api/v1/tenants/{fixture.TenantId}/evidence-redactions/{id}";
        using var prepared = await preparer.PutAsync($"{root}/preparations/{preparation}", Json(new
        {
            expected_revision = 0,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "First reason"
        }));
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var path = $"{root}/approvals/{approval}";

        // Act
        using var first = await approver.PutAsync(path, Json(new { expected_revision = 1, preparation_id = preparation, prepared_revision = 1 }));
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        using var later = await preparer.PutAsync($"{root}/preparations/{Uuid.CreateVersion4()}", Json(new
        {
            expected_revision = 2,
            original_artifact_id = fixture.ArtifactId,
            derived_artifact_id = derived,
            provenance = "Manual",
            reason = "Later reason"
        }));
        using var retry = await approver.PutAsync(path, Json(new { expected_revision = 1, preparation_id = preparation, prepared_revision = 1 }));
        using var current = await approver.GetAsync(root);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(firstBytes, await retry.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var original = JsonDocument.Parse(firstBytes);
        using var latest = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.True(original.RootElement.GetProperty("approval_current").GetBoolean());
        Assert.False(latest.RootElement.GetProperty("approval_current").GetBoolean());
        Assert.Equal(3, history.Facts.Count);
    }

    static WebApplicationFactory<global::Program> Factory(ArtifactMetadataAccessTests.Fixture fixture) =>
        new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "evidence-redaction-native-http-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                    services.Remove(descriptor);
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.RemoveAll<IKvClient>();
                services.RemoveAll<ISourceFactResolver>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(fixture.Provider.GetRequiredService<IEventStore>());
                services.AddSingleton(fixture.Provider.GetRequiredService<IDomainEventReader>());
                services.AddSingleton(fixture.Provider.GetRequiredService<IKvClient>());
                services.AddSingleton(fixture.Provider.GetRequiredService<TimeProvider>());
                services.AddSingleton<ISourceFactResolver>(provider => new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
            });
        });

    static async Task RegisterDeveloperIdentityAsync(ArtifactMetadataAccessTests.Fixture fixture, Uuid actualUser, string email)
    {
        var subject = Uuid.CreateVersion5(DeveloperUserRegistration.IdentifierNamespaceId, email).ToString();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new UserIdentity(DeveloperUserRegistration.Provider, subject),
            identity => identity.Register(actualUser, email));
    }
    static async Task LoginAsync(HttpClient client, string email, Uuid actualUser)
    {
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", Json(new { email_address = email }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(actualUser.ToString(), document.RootElement.GetProperty("user_id").GetString());
    }
    static StringContent Json<T>(T value) => new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
}
