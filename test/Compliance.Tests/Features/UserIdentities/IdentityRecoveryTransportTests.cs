using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>The identity recovery routes: caller-chosen challenge identity, the fixed 202 acknowledgment and retired routes.</summary>
[Trait("Category", "WebIntegration")]
public sealed class IdentityRecoveryTransportTests
{
    const string SessionSecret = "bdgrz-recovery-transport-session-key-01";
    const string ProviderSecret = "oidc-provider-recovery-transport-key-01";
    const string Issuer = "https://issuer.example/";
    const string Email = "recovery-transport@example.com";

    [Fact]
    public async Task ShouldAcceptWithFrozenAcknowledgmentAndClaimOnceGivenExactHttpRetries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, oldIdentity) = await SeedAccountAsync(factory);
        var challengeId = Uuid.CreateVersion4();
        using var started = await client.PutAsJsonAsync($"/api/v1/identity-recovery/challenges/{challengeId}",
            new { email_address = Email });
        var token = await TokenAsync(factory, userId, challengeId);
        var body = new { email_address = Email, token, old_identity_id = oldIdentity };
        var path = $"/api/v1/identity-recovery/challenges/{challengeId}/completion";

        // Act
        using var first = await SendCompletionAsync(client, path, body, "new-subject");
        var firstBody = await first.Content.ReadAsStringAsync();
        using var retry = await SendCompletionAsync(client, path, body, "new-subject");
        var retryBody = await retry.Content.ReadAsStringAsync();
        var facts = await FactsAsync(factory);

        // Assert
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(firstBody, retryBody);
        using var document = JsonDocument.Parse(firstBody);
        Assert.Equal(challengeId.ToString(), document.RootElement.GetProperty("operation_id").GetString());
        Assert.Equal($"/api/v1/platform/operations/{challengeId}", document.RootElement.GetProperty("status_url").GetString());
        Assert.True(document.RootElement.TryGetProperty("accepted_at", out _));
        Assert.Single(facts.OfType<IdentityRecoveryChallengeClaimed>());
        Assert.DoesNotContain(token, firstBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedReplacementUnderTheSameChallengeIdentity()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, oldIdentity) = await SeedAccountAsync(factory);
        var challengeId = Uuid.CreateVersion4();
        using var started = await client.PutAsJsonAsync($"/api/v1/identity-recovery/challenges/{challengeId}",
            new { email_address = Email });
        var token = await TokenAsync(factory, userId, challengeId);
        var body = new { email_address = Email, token, old_identity_id = oldIdentity };
        var path = $"/api/v1/identity-recovery/challenges/{challengeId}/completion";
        using var first = await SendCompletionAsync(client, path, body, "new-subject");

        // Act
        using var changed = await SendCompletionAsync(client, path, body, "attacker-subject");

        // Assert
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Single((await FactsAsync(factory)).OfType<IdentityRecoveryChallengeClaimed>());
    }

    [Fact]
    public async Task ShouldAnswerStartIdenticallyGivenUnknownAndKnownAddress()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAccountAsync(factory);

        // Act
        using var unknown = await client.PutAsJsonAsync($"/api/v1/identity-recovery/challenges/{Uuid.CreateVersion4()}",
            new { email_address = "nobody@example.com" });
        using var known = await client.PutAsJsonAsync($"/api/v1/identity-recovery/challenges/{Uuid.CreateVersion4()}",
            new { email_address = Email });

        // Assert
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ShouldRejectInvalidChallengeIdentityGivenStartOrCompletion(string challengeId)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var start = await client.PutAsJsonAsync($"/api/v1/identity-recovery/challenges/{challengeId}",
            new { email_address = Email });
        using var completion = await SendCompletionAsync(client,
            $"/api/v1/identity-recovery/challenges/{challengeId}/completion",
            new { email_address = Email, token = new string('a', 64), old_identity_id = Uuid.CreateVersion4() }, "new-subject");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, completion.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReplacementProviderProofGivenCompletion()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/identity-recovery/challenges/{Uuid.CreateVersion4()}/completion",
            new { email_address = Email, token = new string('a', 64), old_identity_id = Uuid.CreateVersion4() });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/identity-recovery/challenges")]
    [InlineData("POST", "/api/v1/identity-recovery/completions")]
    public async Task ShouldRetireTheOldRecoveryRoutesGivenIdentifiedRecovery(string method, string path)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { email_address = Email }),
        });

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Empty(await FactsAsync(factory));
    }

    static async Task<HttpResponseMessage> SendCompletionAsync(HttpClient client, string path, object body, string subject)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
            Token(Issuer, "compliance-api", ProviderSecret, subject));
        return await client.SendAsync(request);
    }

    static async Task<(Uuid UserId, Uuid OldIdentity)> SeedAccountAsync(WebApplicationFactory<Program> factory)
    {
        var userId = Uuid.CreateVersion4();
        var old = new UserIdentity(Issuer, "old-subject");
        Assert.True(old.Register(userId, Email).IsSuccess);
        var now = DateTimeOffset.UtcNow;
        const string proof = "previously-verified-email-proof";
        var address = new EmailAddress(Email);
        Assert.True(address.Reserve(userId).IsSuccess);
        Assert.True(address.IssueChallenge(userId, Uuid.CreateVersion4(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof))), now.AddMinutes(15), now).IsSuccess);
        Assert.True(address.CompleteChallenge(userId, proof, now).IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var context = new RequestDispatchContext(RequestActor.System);
        await writer.SaveAsync(old, context, CancellationToken.None);
        await writer.SaveAsync(address, context, CancellationToken.None);
        return (userId, old.Id);
    }

    static async Task<string> TokenAsync(WebApplicationFactory<Program> factory, Uuid userId, Uuid challengeId)
    {
        var issued = (await FactsAsync(factory)).OfType<IdentityRecoveryChallengeIssued>()
            .Single(fact => fact.ChallengeId == challengeId);
        return factory.Services.GetRequiredService<EmailChallengeTokenKeys>()
            .DeriveRecovery(issued.TokenKeyId, challengeId, userId, Email, issued.ExpiresAt);
    }

    static async Task<List<DomainEvent>> FactsAsync(WebApplicationFactory<Program> factory)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new EmailAddress(Email).Stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            ProductionEmailDeliveryTestConfiguration.Apply(builder);
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", Issuer);
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", SessionSecret);
            builder.UseSetting("Compliance:WorkDigest:ApplicationOrigin", "https://app.example");
            builder.UseSetting("Compliance:WorkDigest:ClientWorkItemRouteTemplate",
                "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-recovery-transport");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(item =>
                             item.ServiceType == typeof(IHostedService)).ToArray())
                    services.Remove(descriptor);
                var store = new InMemoryEventStore();
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.RemoveAll<IKvClient>();
                services.AddSingleton<IKvClient>(new InMemoryKvClient());
                services.PostConfigure<JwtBearerOptions>("BdgrzResource0", options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    options.Configuration.SigningKeys.Add(new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(ProviderSecret)));
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(ProviderSecret));
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = "compliance-api";
                });
            });
        });

    static string Token(string issuer, string audience, string secret, string subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, subject)], now.AddMinutes(-1), now.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
