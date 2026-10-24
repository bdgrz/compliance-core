using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.AccessControl;
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

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

[Trait("Category", "WebIntegration")]
public sealed class CampaignTransportTests
{
    const string Base = "/api/v1/tenants/{tenant_id}/programs/{program_id}";

    [Fact]
    public async Task ShouldDescribeCampaignRoutesAsIdentifiedPutOperationsGivenOpenApi()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var immediate = new[]
        {
            Base + "/training-requirements/{requirement_id}",
            Base + "/training-requirements/revise-training-requirement/{command_id}",
            Base + "/policy-campaigns/{campaign_id}",
            Base + "/training-campaigns/{campaign_id}",
            Base + "/campaigns/{campaign_id}/reconciliations/{command_id}",
        };
        var accepted = new[]
        {
            Base + "/campaigns/{campaign_id}/acknowledgements/{command_id}",
            Base + "/campaigns/{campaign_id}/completions/{command_id}",
            Base + "/campaigns/{campaign_id}/waivers/{command_id}",
            Base + "/campaigns/{campaign_id}/closure/{command_id}",
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
            Assert.True(operation.TryGetProperty("requestBody", out _), route);
            Assert.True(operation.GetProperty("responses").TryGetProperty("202", out _), route);
        }

        foreach (var retired in new[]
                 {
                     Base + "/policy-campaigns", Base + "/training-campaigns", Base + "/training-requirements",
                     Base + "/campaigns/{campaign_id}/acknowledgements", Base + "/campaigns/{campaign_id}/completions",
                     Base + "/campaigns/{campaign_id}/waivers", Base + "/campaigns/{campaign_id}/closure",
                     Base + "/campaigns/{campaign_id}/reconciliations",
                 })
            Assert.False(paths.TryGetProperty(retired, out var item) &&
                         (item.TryGetProperty("post", out _) || item.TryGetProperty("put", out _)), retired);
    }

    [Fact]
    public async Task ShouldReplayExactBodyAndConflictChangedIntentGivenTrainingRequirementRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var scope = await ArrangeAsync(factory, client);
        var requirementId = TrainingRequirementIdentity.IdFor(scope.Tenant, scope.Program, "TR-HTTP");
        var define = $"{scope.ProgramPath}/training-requirements/{requirementId}";
        const string body = """{"identifier":"tr-http","content":{"course_name":"Security Awareness","description":null,"audience_kind":"core_security","audience_teams":null,"delivery_source":"manual"}}""";

        // Act
        using var defined = await client.PutAsync(define, Json(body));
        var definedText = await defined.Content.ReadAsStringAsync();
        var revisePath = $"{scope.ProgramPath}/training-requirements/revise-training-requirement/{Guid.NewGuid()}";
        var revise = $$$"""{"requirement_id":"{{{requirementId}}}","expected_version":1,"content":{"course_name":"Security Awareness v2","description":null,"audience_kind":"core_security","audience_teams":null,"delivery_source":"manual"}}""";
        using var revised = await client.PutAsync(revisePath, Json(revise));
        var revisedText = await revised.Content.ReadAsStringAsync();
        using var definedAgain = await client.PutAsync(define, Json(body));
        using var revisedAgain = await client.PutAsync(revisePath, Json(revise));
        using var changed = await client.PutAsync(define, Json(body.Replace("Annual", "Other", StringComparison.Ordinal)
            .Replace("Security Awareness", "Changed", StringComparison.Ordinal)));
        using var mismatched = await client.PutAsync($"{scope.ProgramPath}/training-requirements/{Guid.NewGuid()}",
            Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.OK, defined.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(definedText, await definedAgain.Content.ReadAsStringAsync());
        Assert.Equal(revisedText, await revisedAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);
        using var document = JsonDocument.Parse(revisedText);
        Assert.Equal(2, document.RootElement.GetProperty("version").GetInt64());
        Assert.Equal("TR-HTTP", document.RootElement.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task ShouldAcceptWithFixedAcknowledgmentAndConflictChangedIntentGivenResultAndClosureRoutes()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var scope = await ArrangeAsync(factory, client);
        var campaign = await scope.LaunchAsync(factory);
        var acknowledgementPath = $"{scope.ProgramPath}/campaigns/{campaign}/acknowledgements/{Guid.NewGuid()}";
        var acknowledgement = $$"""{"person_id":"{{scope.Person}}","policy_version":1,"content_sha256":"{{CampaignHost.PolicySubject.ContentSha256}}","acknowledgement_text":"{{PolicyDistributionCampaign.DefaultAcknowledgementText}}","expected_campaign_revision":1}""";

        // Act
        using var accepted = await client.PutAsync(acknowledgementPath, Json(acknowledgement));
        var acceptedText = await accepted.Content.ReadAsStringAsync();
        using var again = await client.PutAsync(acknowledgementPath, Json(acknowledgement));
        using var changed = await client.PutAsync(acknowledgementPath, Json(acknowledgement.Replace(
            CampaignHost.PolicySubject.ContentSha256, new string('c', 64), StringComparison.Ordinal)));
        var closePath = $"{scope.ProgramPath}/campaigns/{campaign}/closure/{Guid.NewGuid()}";
        const string close = """{"rationale":"Done","expected_revision":2}""";
        using var closing = await client.PutAsync(closePath, Json(close));
        var closingText = await closing.Content.ReadAsStringAsync();
        using var closingAgain = await client.PutAsync(closePath, Json(close));
        using var late = await client.PutAsync($"{scope.ProgramPath}/campaigns/{campaign}/acknowledgements/{Guid.NewGuid()}",
            Json(acknowledgement));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using var document = JsonDocument.Parse(acceptedText);
        var statusUrl = document.RootElement.GetProperty("status_url").GetString();
        Assert.EndsWith(document.RootElement.GetProperty("operation_id").GetString()!, statusUrl, StringComparison.Ordinal);
        Assert.Equal(statusUrl, accepted.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Equal(acceptedText, await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, closing.StatusCode);
        Assert.Equal(closingText, await closingAgain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    static async Task<Arranged> ArrangeAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(client,
            $"campaign-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        var (tenant, program, person) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var member = RbacIds.Member(tenant, user);
        var actor = ActorReference.ForMember(member, "Campaign member");
        await ControlOperationE2ETests.SeedAsync(factory, new Member(tenant, user), item => item.Register());
        await ControlOperationE2ETests.SeedAsync(factory, new Person(tenant, person), item =>
        {
            Assert.True(item.Record("Campaign member", null, actor, DateTimeOffset.UtcNow).IsSuccess);
            Assert.Null(item.CorrelateMembership(1, user, actor, DateTimeOffset.UtcNow));
            return Result.Success;
        });
        return new Arranged(tenant, program, person, member, actor);
    }

    static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "campaign-transport-tests");
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

    sealed record Arranged(Uuid Tenant, Uuid Program, Uuid Person, Uuid Member, ActorReference Actor)
    {
        public string ProgramPath => $"/api/v1/tenants/{Tenant}/programs/{Program}";

        public async Task<Uuid> LaunchAsync(WebApplicationFactory<Program> factory)
        {
            var campaign = Uuid.CreateVersion4();
            await CampaignSeed.LaunchAsync(factory.Services, Tenant, Program, campaign, CampaignHost.PolicySubject,
                PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(), new string('d', 64), new RosterAudience(
                    new Dictionary<Uuid, RosterMatch>
                    {
                        [Person] = new(Person, "Campaign member", "employee", null, new DateOnly(2025, 1, 1)),
                    }, new HashSet<Uuid> { Person }), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), null, Actor,
                Member, DateTimeOffset.UtcNow);
            return campaign;
        }
    }
}
