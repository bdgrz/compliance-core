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

            Assert.Equal(verificationCode, await ConsumeAsync(socketPath,
                "email_verification", email));
            Assert.Equal(invitationCode, await ConsumeAsync(socketPath,
                "tenant_invitation", email));
            var lateEmail = $"late-{Guid.NewGuid():N}@example.com";
            var pendingConsumption = ConsumeAsync(socketPath, "tenant_invitation", lateEmail);
            await Task.Delay(50);
            await ((ITenantInvitationDelivery)mailbox).SendAsync(Id, Id, lateEmail,
                lateInvitationCode, CancellationToken.None);
            Assert.Equal(lateInvitationCode, await pendingConsumption);

            var emptyResponse = await ConsumeResponseAsync(socketPath,
                "email_verification", email, timeoutMs: 0);
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

    internal static async Task<string> ConsumeAsync(string socketPath, string delivery, string email)
    {
        var response = await ConsumeResponseAsync(socketPath, delivery, email, timeoutMs: 1000);
        using var document = JsonDocument.Parse(response);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        return document.RootElement.GetProperty("token").GetString()!;
    }

    static async Task<string> ConsumeResponseAsync(string socketPath, string delivery, string email,
        int timeoutMs)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        var request = CreateConsumeRequest(delivery, email, timeoutMs) + "\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request));
        await stream.FlushAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadLineAsync() ?? throw new IOException("Mailbox closed without a response.");
    }

    static string CreateConsumeRequest(string delivery, string email, int timeoutMs) =>
        JsonSerializer.Serialize(new
        {
            op = "consume",
            delivery,
            email,
            timeout_ms = timeoutMs,
        });
}
