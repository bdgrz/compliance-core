using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Features.Providers;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>The email address routes: caller-chosen identities, native facts and exact HTTP retries.</summary>
[Trait("Category", "WebIntegration")]
public sealed class EmailChallengeTransportTests
{
    const string Email = "challenge-transport@example.com";

    [Fact]
    public async Task ShouldAcceptReserveIssueAndVerifyOnceGivenExactHttpRetries()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = Email });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await client.GetAsync("/auth/session");
        var userId = Uuid.Parse((await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        var path = $"/api/v1/users/{userId}/email-addresses/{Email}";
        var reserve = Uuid.CreateVersion4();
        var challenge = Uuid.CreateVersion4();

        // Act
        var reserved = await PutTwiceAsync($"{path}/{reserve}", new { expected_revision = -1 });
        var issued = await PutTwiceAsync($"{path}/challenges/{challenge}", new { });
        var issuedFact = (await FactsAsync(factory)).OfType<EmailChallengeIssued>().Single();
        var token = factory.Services.GetRequiredService<EmailChallengeTokenKeys>()
            .Derive(issuedFact.TokenKeyId, challenge, userId, Email, issuedFact.ExpiresAt);
        var verified = await PutTwiceAsync($"{path}/verifications/{challenge}", new { token });
        using var wrongToken = await client.PutAsJsonAsync($"{path}/verifications/{challenge}", new { token = new string('0', 64) });
        using var status = await client.GetAsync($"{path}/challenges/status");
        var facts = await FactsAsync(factory);

        // Assert
        Assert.All(new[] { reserved, issued, verified }, code => Assert.Equal(HttpStatusCode.OK, code));
        Assert.False(wrongToken.IsSuccessStatusCode);
        Assert.Equal("verified", (await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("delivery_status").GetString());
        Assert.Collection(facts,
            fact => Assert.Equal(reserve, Assert.IsType<EmailAddressReserved>(fact).Operation.LogicalId),
            fact => Assert.Equal(challenge, Assert.IsType<EmailChallengeIssued>(fact).Operation.LogicalId),
            fact => Assert.Equal(challenge, Assert.IsType<EmailAddressVerified>(fact).Operation.LogicalId));
        Assert.DoesNotContain(token, string.Join('\n', facts.Select(fact =>
            JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default.Options))), StringComparison.Ordinal);

        async Task<HttpStatusCode> PutTwiceAsync(string route, object body)
        {
            using var first = await client.PutAsJsonAsync(route, body);
            using var retry = await client.PutAsJsonAsync(route, body);
            Assert.Equal(first.StatusCode, retry.StatusCode);
            return first.StatusCode;
        }
    }

    static async Task<List<DomainEvent>> FactsAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
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
}
