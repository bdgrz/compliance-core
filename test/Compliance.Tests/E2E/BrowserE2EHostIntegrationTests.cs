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
    public async Task ShouldReadCreatorProvisioningStatusGivenVerifiedDeveloperUser()
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
        await WaitForVerifiedAddressAsync(client, addressPath);

        using var unverifiedHandler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var unverifiedClient = new HttpClient(unverifiedHandler) { BaseAddress = browserHost.BaseUri };
        using var unverifiedLogin = await unverifiedClient.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument($"unverified-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(HttpStatusCode.OK, unverifiedLogin.StatusCode);
        using var unverifiedRegistration = await unverifiedClient.PostAsJsonAsync("/api/v1/tenants",
            new
            {
                name = "Unverified browser user organization",
                slug = $"unverified-{Guid.NewGuid():N}"[..24],
                legal_name = "Unverified browser user legal entity",
            });
        Assert.Equal(HttpStatusCode.Forbidden, unverifiedRegistration.StatusCode);

        using var registered = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Browser E2E creator organization",
            slug = $"browser-{Guid.NewGuid():N}"[..24],
            legal_name = "Browser E2E creator legal entity",
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var tenant = await registered.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);

        using var provisioningResponse = await client.GetAsync(
            $"/api/v1/tenants/{tenant.TenantId}/provisioning-status");
        Assert.Equal(HttpStatusCode.OK, provisioningResponse.StatusCode);
        var provisioning = await provisioningResponse.Content
            .ReadFromJsonAsync<TenantProvisioningStatusDocument>();
        Assert.NotNull(provisioning);
        Assert.Equal(tenant.TenantId, provisioning.TenantId);
        Assert.True(provisioning.Status is "provisioning" or "completed");
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

    static async Task WaitForVerifiedAddressAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK &&
                (await response.Content.ReadFromJsonAsync<EmailDocument>())?.Verified == true)
                return;
            await Task.Delay(250);
        }

        Assert.Fail("The developer identity's email address was not verified.");
    }

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);
    sealed record SessionDocument(string Id);
    sealed record VerificationDocument(string Token);
    sealed record EmailDocument(bool Verified);
    sealed record TenantRegistrationDocument(
        [property: JsonPropertyName("tenant_id")] string TenantId, string Slug);
    sealed record TenantProvisioningStatusDocument(
        [property: JsonPropertyName("tenant_id")] string TenantId,
        string Status,
        [property: JsonPropertyName("failure_code")] string? FailureCode);
}
