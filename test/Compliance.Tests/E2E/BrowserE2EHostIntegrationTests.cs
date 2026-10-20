using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BrowserE2EHostIntegrationTests(BrokerStackFixture broker, ITestOutputHelper output)
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

        var initialSlugHttpStatus = -1;
        using (var initialSlugTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            try
            {
                using var initialSlugResponse = await client.GetAsync(
                    $"/api/v1/tenant-slugs/{tenant.Slug}/mine", initialSlugTimeout.Token);
                initialSlugHttpStatus = (int)initialSlugResponse.StatusCode;
            }
            catch (OperationCanceledException) when (initialSlugTimeout.IsCancellationRequested)
            {
                Assert.Fail("Initial creator slug resolver timed out: http=request_timeout.");
            }
            catch (HttpRequestException)
            {
                Assert.Fail("Initial creator slug resolver failed: http=transport_error.");
            }
        }
        Assert.True(initialSlugHttpStatus is (int)HttpStatusCode.NotFound or (int)HttpStatusCode.OK,
            $"Unexpected initial creator slug resolver HTTP status {initialSlugHttpStatus}.");

        var provisioning = await WaitForCompletedProvisioningAsync(client, tenant.TenantId,
            session.Id, tenant.Slug, browserHost, output);
        Assert.Equal(tenant.TenantId, provisioning.TenantId);
        Assert.Equal("completed", provisioning.Status);

        var slugResolution = await WaitForCreatorSlugResolutionAsync(client, tenant.Slug,
            tenant.TenantId, session.Id, browserHost, initialSlugHttpStatus, output);
        Assert.Equal(tenant.TenantId, slugResolution.TenantId);
        Assert.Equal(tenant.Slug, slugResolution.CurrentSlug);
        Assert.False(slugResolution.Redirect);
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

    static async Task<TenantProvisioningStatusDocument> WaitForCompletedProvisioningAsync(
        HttpClient client, string tenantId, string userId, string slug,
        BrowserE2EHost browserHost, ITestOutputHelper output)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var lastHttpStatus = 0;
        var lastStatus = "unavailable";
        var reads = 0;
        var statusProgression = new List<string>();
        while (DateTimeOffset.UtcNow < deadline)
        {
            reads++;
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                using var response = await client.GetAsync(
                    $"/api/v1/tenants/{tenantId}/provisioning-status", requestTimeout.Token);
                lastHttpStatus = (int)response.StatusCode;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var provisioning = await response.Content
                        .ReadFromJsonAsync<TenantProvisioningStatusDocument>(requestTimeout.Token);
                    if (provisioning is not null)
                    {
                        lastStatus = provisioning.Status;
                        if (statusProgression.Count == 0 || statusProgression[^1] != lastStatus)
                            statusProgression.Add(lastStatus);
                        if (lastStatus == "completed")
                        {
                            output.WriteLine($"Creator provisioning status: {string.Join("->", statusProgression)}; " +
                                $"reads={reads}; elapsed_ms={System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}.");
                            return provisioning;
                        }

                        if (lastStatus == "rejected")
                        {
                            var rejectedSnapshot = await ReadReadinessSnapshotAsync(browserHost,
                                tenantId, userId, slug);
                            Assert.Fail($"Creator provisioning was rejected: http={lastHttpStatus}; " +
                                $"progression={string.Join("->", statusProgression)}; reads={reads}; " +
                                $"{rejectedSnapshot}.");
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (requestTimeout.IsCancellationRequested)
            {
                lastHttpStatus = -1;
                lastStatus = "request_timeout";
                break;
            }
            catch (HttpRequestException)
            {
                lastHttpStatus = -1;
                lastStatus = "transport_error";
                break;
            }

            await Task.Delay(250);
        }

        var snapshot = await ReadReadinessSnapshotAsync(browserHost, tenantId, userId, slug);
        Assert.Fail($"Provisioning did not settle: http={lastHttpStatus}; status={lastStatus}; " +
            $"progression={string.Join("->", statusProgression)}; reads={reads}; {snapshot}.");
        throw new InvalidOperationException("Unreachable after failed assertion.");
    }

    static async Task<TenantSlugResolutionDocument> WaitForCreatorSlugResolutionAsync(
        HttpClient client, string slug, string tenantId, string userId, BrowserE2EHost browserHost,
        int initialHttpStatus, ITestOutputHelper output)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var lastHttpStatus = 0;
        var reads = 0;
        var statusProgression = new List<int> { initialHttpStatus };
        while (DateTimeOffset.UtcNow < deadline)
        {
            reads++;
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                using var response = await client.GetAsync(
                    $"/api/v1/tenant-slugs/{slug}/mine", requestTimeout.Token);
                lastHttpStatus = (int)response.StatusCode;
                if (statusProgression[^1] != lastHttpStatus)
                    statusProgression.Add(lastHttpStatus);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var resolution = await response.Content.ReadFromJsonAsync<TenantSlugResolutionDocument>(
                        requestTimeout.Token);
                    if (resolution is not null)
                    {
                        output.WriteLine($"Creator slug resolution HTTP: {string.Join("->", statusProgression)}; " +
                            $"reads={reads}; elapsed_ms={System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}.");
                        return resolution;
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (requestTimeout.IsCancellationRequested)
            {
                lastHttpStatus = -1;
                break;
            }
            catch (HttpRequestException)
            {
                lastHttpStatus = -1;
                break;
            }

            await Task.Delay(250);
        }

        var snapshot = await ReadReadinessSnapshotAsync(browserHost, tenantId, userId, slug);
        Assert.Fail($"Creator slug resolution remained http={lastHttpStatus}; " +
            $"progression={string.Join("->", statusProgression)}; reads={reads}; {snapshot}.");
        throw new InvalidOperationException("Unreachable after failed assertion.");
    }

    static async Task<ReadinessSnapshot> ReadReadinessSnapshotAsync(
        BrowserE2EHost browserHost, string tenantIdText, string userIdText, string slug)
    {
        var tenantId = Uuid.Parse(tenantIdText, CultureInfo.InvariantCulture);
        var userId = Uuid.Parse(userIdText, CultureInfo.InvariantCulture);
        await using var scope = browserHost.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var memberships = scope.ServiceProvider.GetRequiredService<ITenantMembershipDirectoryReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), CancellationToken.None);
        var slugAggregate = await reader.HydrateAsync(new TenantSlug(slug), CancellationToken.None);
        var member = await reader.HydrateAsync(new Member(tenantId, userId), CancellationToken.None);
        var memberId = RbacIds.Member(tenantId, userId);
        var administratorAssignment = await reader.HydrateAsync(new TeamMember(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), memberId), CancellationToken.None);
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, CancellationToken.None);
        var tenantSlugConfirmed = false;
        var tenantActivated = false;
        var sourceRecordsRead = 0;
        await foreach (var record in events.ReadAsync(new EventStreamAddress("bdgrz", "tenants",
                           tenantId.ToString()), 0, CancellationToken.None))
        {
            tenantSlugConfirmed |= record.Event is TenantSlugConfirmed;
            tenantActivated |= record.Event is TenantActivated;
            sourceRecordsRead++;
            if (sourceRecordsRead >= 32 || tenantSlugConfirmed && tenantActivated)
                break;
        }

        return new ReadinessSnapshot(
            tenant.IsRegistered,
            tenantSlugConfirmed,
            tenantActivated,
            tenant.HasCompletedActivation,
            tenant.IsActive,
            string.Equals(tenant.CurrentSlug, slug, StringComparison.Ordinal),
            slugAggregate.OwningTenantId == tenantId,
            member.IsRegistered,
            member.IsAccessEligibleOn(DateOnly.FromDateTime(DateTime.UtcNow)),
            membership is not null && membership.TenantId == tenantId,
            administratorAssignment.IsAssigned);
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
    sealed record TenantSlugResolutionDocument(
        [property: JsonPropertyName("tenant_id")] string TenantId,
        [property: JsonPropertyName("current_slug")] string CurrentSlug,
        bool Redirect);
    sealed record ReadinessSnapshot(
        bool TenantRegistered,
        bool TenantSlugConfirmed,
        bool TenantActivated,
        bool TenantHasCompletedActivation,
        bool TenantActive,
        bool TenantSlugMatches,
        bool SlugOwnerMatches,
        bool MemberRegistered,
        bool MemberEligible,
        bool MembershipProjected,
        bool AdministratorTeamMemberAssigned);
}
