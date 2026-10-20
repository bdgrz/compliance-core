using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

public sealed class BrowserE2EBridgeTests
{
    static readonly Uuid Id = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d",
        CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldBindLoopbackOnlyAndForwardRequestGivenTestServer()
    {
        // Arrange
        using var testServerHost = await new HostBuilder().ConfigureWebHost(webHost =>
            webHost.UseTestServer().Configure(app => app.Run(async context =>
            {
                context.Response.Headers["X-TestServer"] = "reached";
                await context.Response.WriteAsync(
                    $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
            }))).StartAsync();
        var testServer = testServerHost.GetTestServer();
        await using var bridge = await LoopbackTestServerProxy.StartAsync(testServer.CreateHandler());
        Assert.Equal(IPAddress.Loopback, IPAddress.Parse(bridge.BaseUri.Host));
        using var client = new HttpClient { BaseAddress = bridge.BaseUri };

        // Act
        using var response = await client.GetAsync("/bridge-check?value=forwarded");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reached", response.Headers.GetValues("X-TestServer").Single());
        Assert.Equal("GET /bridge-check?value=forwarded",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ShouldConsumeVerificationAndInvitationCodesOverPrivateSocketGivenStartedMailbox()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        var socketPath = mailbox.SocketPath;
        var directoryPath = Path.GetDirectoryName(socketPath)!;
        var email = $"browser-{Guid.NewGuid():N}@example.com";
        const string verificationCode = "verify-code-must-not-be-logged";
        const string invitationCode = "invite-code-must-not-be-logged";
        const string lateInvitationCode = "late-invitation-code";
        try
        {
            // Act
            await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id, email,
                verificationCode, CancellationToken.None);
            await ((ITenantInvitationDelivery)mailbox).SendAsync(Id, Id, email,
                invitationCode, CancellationToken.None);

            var invitationWithoutTenant = await ConsumeResponseAsync(socketPath,
                "tenant_invitation", email, timeoutMs: 0);
            using (var invalidTenantDocument = JsonDocument.Parse(invitationWithoutTenant))
                Assert.Equal("invalid_request",
                    invalidTenantDocument.RootElement.GetProperty("status").GetString());

            Assert.Equal(verificationCode, await ConsumeAsync(socketPath,
                "email_verification", email, Id.ToString()));
            Assert.Equal(invitationCode, await ConsumeAsync(socketPath,
                "tenant_invitation", email, Id.ToString()));
            var lateEmail = $"late-{Guid.NewGuid():N}@example.com";
            var pendingConsumption = ConsumeAsync(socketPath, "tenant_invitation", lateEmail,
                Id.ToString());
            await Task.Delay(50);
            await ((ITenantInvitationDelivery)mailbox).SendAsync(Id, Id, lateEmail,
                lateInvitationCode, CancellationToken.None);
            Assert.Equal(lateInvitationCode, await pendingConsumption);

            var emptyResponse = await ConsumeResponseAsync(socketPath,
                "email_verification", email, timeoutMs: 0, scopeId: Id.ToString());
            using var emptyDocument = JsonDocument.Parse(emptyResponse);
            Assert.Equal("timeout", emptyDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directoryPath));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(socketPath));
        }
        finally
        {
            await mailbox.DisposeAsync();
        }

        // Assert
        Assert.False(File.Exists(socketPath));
        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public async Task ShouldIsolateInvitationTokensByTenantGivenSameEmail()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        var email = $"shared-{Guid.NewGuid():N}@example.com";
        var otherTenantId = Uuid.Parse("1862062f-97e9-45de-a312-0f884c48180d",
            CultureInfo.InvariantCulture);
        const string firstTenantToken = "first-tenant-token";
        const string secondTenantToken = "second-tenant-token";
        await ((ITenantInvitationDelivery)mailbox).SendAsync(Id, Id, email,
            firstTenantToken, CancellationToken.None);
        await ((ITenantInvitationDelivery)mailbox).SendAsync(Id, otherTenantId, email,
            secondTenantToken, CancellationToken.None);

        // Act
        var secondTenantResult = await ConsumeAsync(mailbox.SocketPath, "tenant_invitation", email,
            otherTenantId.ToString());
        var firstTenantResult = await ConsumeAsync(mailbox.SocketPath, "tenant_invitation", email,
            Id.ToString());

        // Assert
        Assert.Equal(secondTenantToken, secondTenantResult);
        Assert.Equal(firstTenantToken, firstTenantResult);
    }

    [Fact]
    public async Task ShouldIsolateVerificationTokensByUserGivenSameEmail()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        var email = $"shared-user-{Guid.NewGuid():N}@example.com";
        var otherUserId = Uuid.Parse("3862062f-97e9-45de-a312-0f884c48180d",
            CultureInfo.InvariantCulture);
        const string firstUserToken = "first-user-token";
        const string secondUserToken = "second-user-token";
        await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id, email, firstUserToken,
            CancellationToken.None);
        await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, otherUserId, email, secondUserToken,
            CancellationToken.None);

        // Act
        var secondUserResult = await ConsumeAsync(mailbox.SocketPath, "email_verification", email,
            otherUserId.ToString());
        var firstUserResult = await ConsumeAsync(mailbox.SocketPath, "email_verification", email,
            Id.ToString());

        // Assert
        Assert.Equal(secondUserToken, secondUserResult);
        Assert.Equal(firstUserToken, firstUserResult);

        const string firstRecoveryToken = "first-user-recovery-token";
        const string secondRecoveryToken = "second-user-recovery-token";
        await ((IEmailChallengeDelivery)mailbox).SendRecoveryAsync(Id, Id, email,
            firstRecoveryToken, CancellationToken.None);
        await ((IEmailChallengeDelivery)mailbox).SendRecoveryAsync(Id, otherUserId, email,
            secondRecoveryToken, CancellationToken.None);
        Assert.Equal(secondRecoveryToken, await ConsumeAsync(mailbox.SocketPath,
            "email_recovery", email, otherUserId.ToString()));
        Assert.Equal(firstRecoveryToken, await ConsumeAsync(mailbox.SocketPath,
            "email_recovery", email, Id.ToString()));
    }

    [Fact]
    public async Task ShouldReturnLatestTokenGivenReissuedVerificationChallenge()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        var email = $"reissued-{Guid.NewGuid():N}@example.com";
        const string earlierToken = "earlier-challenge-token";
        const string latestToken = "latest-challenge-token";
        await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id, email, earlierToken,
            CancellationToken.None);
        var laterChallengeId = Uuid.Parse("2862062f-97e9-45de-a312-0f884c48180d",
            CultureInfo.InvariantCulture);
        await ((IEmailChallengeDelivery)mailbox).SendAsync(laterChallengeId, Id, email, latestToken,
            CancellationToken.None);

        // Act
        var result = await ConsumeAsync(mailbox.SocketPath, "email_verification", email,
            Id.ToString());

        // Assert
        Assert.Equal(latestToken, result);
    }

    [Fact]
    public async Task ShouldBoundMailboxKeysAndReleaseCapacityGivenConsume()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        const int maximumKeys = 256;
        for (var index = 0; index < maximumKeys; index++)
        {
            await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id,
                $"capacity-{index}@example.com", $"token-{index}", CancellationToken.None);
        }

        // Act
        var zeroTimeoutResponse = await ConsumeResponseAsync(mailbox.SocketPath,
            "email_verification", "zero-timeout-miss@example.com", timeoutMs: 0,
            scopeId: Id.ToString());
        var fullResponse = await ConsumeResponseAsync(mailbox.SocketPath,
            "email_verification", "full-mailbox-miss@example.com", timeoutMs: 1,
            scopeId: Id.ToString());
        var overflowEmail = "overflow@example.com";
        const string overflowToken = "must-not-be-in-capacity-exception";
        var capacityException = await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
            await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id, overflowEmail,
                overflowToken, CancellationToken.None));

        // Assert
        using (var zeroTimeoutDocument = JsonDocument.Parse(zeroTimeoutResponse))
            Assert.Equal("timeout", zeroTimeoutDocument.RootElement.GetProperty("status").GetString());
        using (var fullDocument = JsonDocument.Parse(fullResponse))
            Assert.Equal("capacity_exceeded", fullDocument.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain(overflowEmail, capacityException.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(overflowToken, capacityException.Message, StringComparison.Ordinal);

        // Consuming a pending token releases its key slot for another delivery.
        Assert.Equal("token-0", await ConsumeAsync(mailbox.SocketPath,
            "email_verification", "capacity-0@example.com", Id.ToString()));
        await ((IEmailChallengeDelivery)mailbox).SendAsync(Id, Id, overflowEmail, overflowToken,
            CancellationToken.None);
        Assert.Equal(overflowToken, await ConsumeAsync(mailbox.SocketPath,
            "email_verification", overflowEmail, Id.ToString()));
    }

    [Fact]
    public async Task ShouldReleaseTimedOutMailboxKeysAndPreserveRacingDeliveryGivenSameUser()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();
        var delivery = (IEmailChallengeDelivery)mailbox;
        var email = $"timeout-race-{Guid.NewGuid():N}@example.com";

        // Act: exercise timeout cleanup, then race each delivery with a pending consume. Whether
        // the consumer or sender linearizes first, the token must be delivered exactly once.
        for (var index = 0; index < 256; index++)
        {
            var missedEmail = $"timeout-miss-{index}-{Guid.NewGuid():N}@example.com";
            var timeout = await ConsumeResponseAsync(mailbox.SocketPath, "email_verification",
                missedEmail, timeoutMs: 1, scopeId: Id.ToString());
            using var timeoutDocument = JsonDocument.Parse(timeout);
            Assert.Equal("timeout", timeoutDocument.RootElement.GetProperty("status").GetString());
        }

        for (var index = 0; index < 20; index++)
        {
            var token = $"racing-token-{index}";
            var consume = ConsumeResponseAsync(mailbox.SocketPath, "email_verification", email,
                timeoutMs: 5, scopeId: Id.ToString());
            await Task.Delay(4);
            await delivery.SendAsync(Id, Id, email, token, CancellationToken.None);
            var response = await consume;
            using var document = JsonDocument.Parse(response);
            var status = document.RootElement.GetProperty("status").GetString();
            if (status == "ok")
            {
                Assert.Equal(token, document.RootElement.GetProperty("token").GetString());
            }
            else
            {
                Assert.Equal("timeout", status);
                Assert.Equal(token, await ConsumeAsync(mailbox.SocketPath, "email_verification", email,
                    Id.ToString()));
            }
        }

        // Assert: timed-out empty keys no longer occupy the configured 256-key limit.
        const string afterTimeouts = "available-after-timeouts";
        await delivery.SendAsync(Id, Id, "fresh-after-timeouts@example.com", afterTimeouts,
            CancellationToken.None);
        Assert.Equal(afterTimeouts, await ConsumeAsync(mailbox.SocketPath, "email_verification",
            "fresh-after-timeouts@example.com", Id.ToString()));
    }

    [Fact]
    public async Task ShouldReleaseMailboxKeyGivenDeliverySatisfiesWaitingConsumer()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox(maximumConcurrentConnections: 2,
            requestLineReadDeadline: TimeSpan.FromSeconds(5), maximumDeliveryKeys: 1);
        await mailbox.StartAsync();
        var delivery = (IEmailChallengeDelivery)mailbox;
        var waitingEmail = $"waiting-{Guid.NewGuid():N}@example.com";
        var pending = ConsumeResponseAsync(mailbox.SocketPath, "email_verification", waitingEmail,
            timeoutMs: 5000, scopeId: Id.ToString());
        await Task.Delay(50);

        // Act
        const string waitingToken = "delivered-to-waiter";
        await delivery.SendAsync(Id, Id, waitingEmail, waitingToken, CancellationToken.None);
        var response = await pending;
        const string nextToken = "slot-reused-after-delivery";
        await delivery.SendAsync(Id, Id, "after-waiter@example.com", nextToken,
            CancellationToken.None);

        // Assert
        using var document = JsonDocument.Parse(response);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(waitingToken, document.RootElement.GetProperty("token").GetString());
        Assert.Equal(nextToken, await ConsumeAsync(mailbox.SocketPath, "email_verification",
            "after-waiter@example.com", Id.ToString()));
    }

    [Fact]
    public async Task ShouldRejectMissingOrMalformedDeliveryScopeGivenConsumeRequest()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox();
        await mailbox.StartAsync();

        // Act
        var missingUser = await ConsumeResponseAsync(mailbox.SocketPath, "email_verification",
            "scope@example.com", timeoutMs: 0);
        var malformedUser = await ConsumeResponseAsync(mailbox.SocketPath, "email_recovery",
            "scope@example.com", timeoutMs: 0, scopeId: "not-a-uuid");
        var malformedTenant = await ConsumeResponseAsync(mailbox.SocketPath, "tenant_invitation",
            "scope@example.com", timeoutMs: 0, scopeId: "not-a-uuid");

        // Assert
        foreach (var response in new[] { missingUser, malformedUser, malformedTenant })
        {
            using var document = JsonDocument.Parse(response);
            Assert.Equal("invalid_request", document.RootElement.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task ShouldRejectStalledRequestLineGivenReadDeadlineExpires()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox(maximumConcurrentConnections: 2,
            requestLineReadDeadline: TimeSpan.FromMilliseconds(150));
        await mailbox.StartAsync();
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(mailbox.SocketPath));
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"op\":"));
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // Act
        var response = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.NotNull(response);
        using var document = JsonDocument.Parse(response);
        Assert.Equal("invalid_request", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldRejectClientAboveConnectionLimitGivenOneStalledClient()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        await using var mailbox = new UnixDomainTokenMailbox(maximumConcurrentConnections: 1,
            requestLineReadDeadline: TimeSpan.FromSeconds(5));
        await mailbox.StartAsync();
        using var stalledSocket = new Socket(AddressFamily.Unix, SocketType.Stream,
            ProtocolType.Unspecified);
        await stalledSocket.ConnectAsync(new UnixDomainSocketEndPoint(mailbox.SocketPath));
        await using var stalledStream = new NetworkStream(stalledSocket, ownsSocket: false);
        await stalledStream.WriteAsync(Encoding.UTF8.GetBytes("{\"op\":"));

        using var excessSocket = new Socket(AddressFamily.Unix, SocketType.Stream,
            ProtocolType.Unspecified);
        await excessSocket.ConnectAsync(new UnixDomainSocketEndPoint(mailbox.SocketPath));
        await using var excessStream = new NetworkStream(excessSocket, ownsSocket: false);
        using var reader = new StreamReader(excessStream, Encoding.UTF8, leaveOpen: true);

        // Act
        var response = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.NotNull(response);
        using var document = JsonDocument.Parse(response);
        Assert.Equal("busy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldDisposeAcceptedSocketGivenShutdownBeforeConnectionAdmission()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Arrange
        var acceptedSocketSource = new TaskCompletionSource<Socket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var mailbox = new UnixDomainTokenMailbox(maximumConcurrentConnections: 1,
            requestLineReadDeadline: TimeSpan.FromSeconds(5), afterAcceptAsync: async (socket, ct) =>
            {
                acceptedSocketSource.TrySetResult(socket);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
            });
        await mailbox.StartAsync();
        using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream,
            ProtocolType.Unspecified);
        await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(mailbox.SocketPath));
        var acceptedSocket = await acceptedSocketSource.Task.WaitAsync(TimeSpan.FromSeconds(2));

        try
        {
            // Act
            await mailbox.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            Assert.True(acceptedSocket.SafeHandle.IsClosed);
        }
        finally
        {
            acceptedSocket.Dispose();
        }
    }

    internal static async Task<string> ConsumeAsync(string socketPath, string delivery, string email,
        string? scopeId = null)
    {
        var response = await ConsumeResponseAsync(socketPath, delivery, email, timeoutMs: 1000,
            scopeId);
        using var document = JsonDocument.Parse(response);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        return document.RootElement.GetProperty("token").GetString()!;
    }

    static async Task<string> ConsumeResponseAsync(string socketPath, string delivery, string email,
        int timeoutMs, string? scopeId = null)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        var request = CreateConsumeRequest(delivery, email, timeoutMs, scopeId) + "\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request));
        await stream.FlushAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadLineAsync() ?? throw new IOException("Mailbox closed without a response.");
    }

    static string CreateConsumeRequest(string delivery, string email, int timeoutMs,
        string? scopeId = null)
    {
        var request = new Dictionary<string, object?>
        {
            ["op"] = "consume",
            ["delivery"] = delivery,
            ["email"] = email,
            ["timeout_ms"] = timeoutMs,
        };
        if (scopeId is not null)
            request[delivery == "tenant_invitation" ? "tenant_id" : "user_id"] = scopeId;
        return JsonSerializer.Serialize(request);
    }
}
