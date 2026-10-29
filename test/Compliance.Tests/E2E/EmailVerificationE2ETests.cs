using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class EmailVerificationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldDeliverLaterUserGivenFailedChallengeAndExplicitReissue()
    {
        // Arrange
        var delivery = new RecoveringDelivery();
        await using var factory = E2EAppFactory.Create(broker).WithWebHostBuilder(host =>
            host.ConfigureTestServices(services =>
                services.AddSingleton<IEmailChallengeDelivery>(delivery)));
        using var client = factory.CreateClient();
        var email = $"retry-{Guid.NewGuid():N}@example.com";
        var userId = await TenantInvitationE2ETests.LoginAsync(client, email);
        var path = $"/api/v1/users/{userId}/email-addresses/{email}";
        var reservationDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < reservationDeadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }

        // Act
        using var issued = await client.PutAsync($"{path}/challenges/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        await WaitForDeliveryStatusAsync(client, path, "failed");
        delivery.AllowSuccess = true;
        using var laterClient = factory.CreateClient();
        var laterEmail = $"later-{Guid.NewGuid():N}@example.com";
        var laterId = await TenantInvitationE2ETests.LoginAsync(laterClient, laterEmail);
        var laterPath = $"/api/v1/users/{laterId}/email-addresses/{laterEmail}";
        await WaitForDeliveryStatusAsync(laterClient, laterPath, "not_issued");
        using var laterIssued = await laterClient.PutAsync($"{laterPath}/challenges/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NoContent, laterIssued.StatusCode);
        await WaitForDeliveryStatusAsync(laterClient, laterPath, "delivered");
        await WaitForDeliveryStatusAsync(client, path, "failed");
        using var reissued = await client.PutAsync($"{path}/challenges/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NoContent, reissued.StatusCode);
        await WaitForDeliveryStatusAsync(client, path, "delivered");
        var attempts = delivery.Attempts.ToArray();
        var ownerAttempts = attempts.Where(attempt => attempt.EmailAddress == email).ToArray();

        // Assert
        Assert.Equal(2, ownerAttempts.Length);
        Assert.Equal(2, ownerAttempts.Select(attempt => attempt.ChallengeId).Distinct().Count());
        Assert.Single(attempts, attempt => attempt.EmailAddress == laterEmail);
        using var completed = await client.PutAsJsonAsync(
            $"{path}/verifications/{ownerAttempts[1].ChallengeId}", new { token = ownerAttempts[1].Token });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        await WaitForDeliveryStatusAsync(client, path, "verified");
    }

    static async Task WaitForDeliveryStatusAsync(HttpClient client, string path, string expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}/challenges/status");
            if (response.StatusCode == HttpStatusCode.OK &&
                (await response.Content.ReadFromJsonAsync<EmailChallengeStatusDocument>())?
                .DeliveryStatus == expected)
                return;
            await Task.Delay(250);
        }
        Assert.Fail($"Email challenge status did not become {expected}.");
    }

    sealed class RecoveringDelivery : IEmailChallengeDelivery
    {
        volatile bool _allowSuccess;

        public ConcurrentQueue<(Uuid ChallengeId, string EmailAddress, string Token)> Attempts { get; } = new();
        public bool AllowSuccess { get => _allowSuccess; set => _allowSuccess = value; }

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = userId;
            ct.ThrowIfCancellationRequested();
            Attempts.Enqueue((challengeId, emailAddress, token));
            if (!AllowSuccess)
                throw new EmailChallengeDeliveryException(EmailChallengeFailureCertainty.DefinitePermanent,
                    "smtp_permanent_rejection");
            return ValueTask.CompletedTask;
        }

        public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct) => SendAsync(challengeId, userId, emailAddress, token, ct);

        public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
            string emailAddress, CancellationToken ct)
        {
            _ = challengeId;
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task ShouldRejectReservationGivenAddressOwnedByAnotherUser()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var ownedEmail = $"owned-{Guid.NewGuid():N}@example.com";

        // Act
        using var firstLogin = await firstClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument(ownedEmail));

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        using var firstSession = await firstClient.GetAsync("/auth/session");
        var first = await firstSession.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(first);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var reserved = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await firstClient.GetAsync(
                $"/api/v1/users/{first.Id}/email-addresses/{ownedEmail}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                reserved = true;
                break;
            }
            await Task.Delay(250);
        }
        Assert.True(reserved);

        using var secondLogin = await secondClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument($"other-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        using var secondSession = await secondClient.GetAsync("/auth/session");
        var second = await secondSession.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(second);

        using var conflict = await secondClient.PutAsJsonAsync(
            $"/api/v1/users/{second.Id}/email-addresses/{ownedEmail}/{Guid.NewGuid()}", new { expected_revision = 0 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ShouldReserveAndVerifyAddressGivenMockDelivery()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        // Act
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument(email));

        // Assert
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await client.GetAsync("/auth/session");
        var identity = await session.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(identity);

        var path = $"/api/v1/users/{identity.Id}/email-addresses/{email}";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        HttpResponseMessage? get = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            get?.Dispose();
            get = await client.GetAsync(path);
            if (get.StatusCode == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.NotNull(get);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var reserved = await get.Content.ReadFromJsonAsync<EmailDocument>();
        Assert.NotNull(reserved);
        Assert.False(reserved.Verified);
        get.Dispose();

        using var forbidden = await client.PutAsync(
            $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenList = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}/email-addresses");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenList.StatusCode);
        using var forbiddenStatus = await client.GetAsync(
            $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges/status");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenStatus.StatusCode);

        var challengeId = Guid.NewGuid();
        using var issued = await client.PutAsync($"{path}/challenges/{challengeId}", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var delivery = factory.Services.GetRequiredService<MockEmailChallengeDelivery>();
        string? token = null;
        var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deliveryDeadline &&
               !delivery.TryGetLatest(Uuid.Parse(identity.Id, CultureInfo.InvariantCulture),
                   email, out token))
            await Task.Delay(250);
        Assert.NotNull(token);

        using var status = await client.GetAsync($"{path}/challenges/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var statusBody = await status.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, statusBody, StringComparison.Ordinal);
        Assert.DoesNotContain(email, statusBody, StringComparison.Ordinal);

        using var completed = await client.PutAsJsonAsync($"{path}/verifications/{challengeId}", new { token });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        EmailDocument? verified = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                verified = await response.Content.ReadFromJsonAsync<EmailDocument>();
                if (verified?.Verified == true)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.True(verified?.Verified);
        using var verifiedSession = await client.GetAsync("/auth/session");
        var sessionState = await verifiedSession.Content.ReadFromJsonAsync<VerifiedSessionDocument>();
        Assert.True(sessionState?.EmailAddressVerified);

        var secondEmail = $"second-{Guid.NewGuid():N}@example.com";
        var secondPath = $"/api/v1/users/{identity.Id}/email-addresses/{secondEmail}";
        using var reservedSecond = await client.PutAsJsonAsync($"{secondPath}/{Guid.NewGuid()}",
            new { expected_revision = 0 });
        Assert.Equal(HttpStatusCode.NoContent, reservedSecond.StatusCode);
        var secondChallengeId = Guid.NewGuid();
        using var issuedSecond = await client.PutAsync($"{secondPath}/challenges/{secondChallengeId}", null);
        Assert.Equal(HttpStatusCode.NoContent, issuedSecond.StatusCode);
        string? secondToken = null;
        var secondDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < secondDeadline &&
               !delivery.TryGetLatest(Uuid.Parse(identity.Id, CultureInfo.InvariantCulture),
                   secondEmail, out secondToken))
            await Task.Delay(250);
        Assert.NotNull(secondToken);
        using var completedSecond = await client.PutAsJsonAsync(
            $"{secondPath}/verifications/{secondChallengeId}", new { token = secondToken });
        Assert.Equal(HttpStatusCode.NoContent, completedSecond.StatusCode);

        EmailListDocument? addresses = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/users/{identity.Id}/email-addresses");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                addresses = await response.Content.ReadFromJsonAsync<EmailListDocument>();
                if (addresses?.Items.Count == 2 && addresses.Items.All(item => item.Verified))
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(addresses);
        Assert.Equal(2, addresses.Items.Count);
        Assert.All(addresses.Items, item => Assert.True(item.Verified));
    }

    sealed record RegistrationDocument([property: JsonPropertyName("email_address")] string EmailAddress);
    sealed record SessionDocument(string Id);
    sealed record EmailChallengeStatusDocument(
        [property: JsonPropertyName("delivery_status")] string DeliveryStatus);
    sealed record VerifiedSessionDocument(
        [property: JsonPropertyName("email_address_verified")] bool EmailAddressVerified);
    sealed record EmailDocument(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("email_address")] string EmailAddress,
        bool Verified);
    sealed record EmailListDocument(IReadOnlyList<EmailDocument> Items);
}
