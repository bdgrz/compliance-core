using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BrowserE2EHostIntegrationTests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldProxyDeveloperLoginAndConsumeVerificationCodeGivenRunningBrowserHost()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var browserHost = await BrowserE2EHost.StartForTestingAsync(broker.WebSocketEndpoint);
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = browserHost.BaseUri };
        var email = $"browser-bridge-{Guid.NewGuid():N}@example.com";

        // Act
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument(email));

        // Assert
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var sessionResponse = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(session);
        var addressPath = $"/api/v1/users/{session.Id}/email-addresses/{email}";
        await WaitForAddressReservationAsync(client, addressPath);

        using var issued = await client.PostAsync($"{addressPath}/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var token = await BrowserE2EBridgeTests.ConsumeAsync(browserHost.MailboxSocketPath,
            "email_verification", email, session.Id);
        using var verified = await client.PostAsJsonAsync($"{addressPath}/verifications",
            new VerificationDocument(token));

        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    static async Task WaitForAddressReservationAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return;
            await Task.Delay(250);
        }

        Assert.Fail("The developer identity's email address was not reserved.");
    }

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);
    sealed record SessionDocument(string Id);
    sealed record VerificationDocument(string Token);
}
