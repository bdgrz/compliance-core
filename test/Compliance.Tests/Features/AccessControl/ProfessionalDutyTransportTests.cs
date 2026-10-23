using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

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
