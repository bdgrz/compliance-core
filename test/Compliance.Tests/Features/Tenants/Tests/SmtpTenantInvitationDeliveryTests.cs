using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

[Collection(SmtpInvitationCollectionDefinition.Name)]
public sealed class SmtpTenantInvitationDeliveryTests
{
    [Fact]
    public async Task ShouldDeliverInvitationWithStableMessageIdentityGivenSameAttemptRetried()
    {
        // Arrange
        await using var inbox = new LoopbackSmtpInbox(connectionCount: 2);
        var settings = new EmailChallengeDeliverySettings("smtp", "127.0.0.1", inbox.Port,
            "sender@example.com", null, null);
        var delivery = new SmtpTenantInvitationDelivery(settings);
        var attemptId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        const string recipient = "collaborator@example.com";
        const string token = "controlled-invitation-code";
        var validationCount = 0;

        // SmtpClient exposes its TLS callback through this obsolete API. This collection
        // runs without parallel tests; pin only this ephemeral certificate and restore it.
#pragma warning disable SYSLIB0014
        var previousValidation = ServicePointManager.ServerCertificateValidationCallback;
        ServicePointManager.ServerCertificateValidationCallback = (_, certificate, _, errors) =>
        {
            Interlocked.Increment(ref validationCount);
            return certificate is not null
                   && errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors
                   && certificate.GetRawCertData().AsSpan().SequenceEqual(inbox.Certificate.RawData);
        };
#pragma warning restore SYSLIB0014
        try
        {
            // Act
            await delivery.SendAsync(attemptId, tenantId, recipient, token, inbox.CancellationToken);
            await delivery.SendAsync(attemptId, tenantId, recipient, token, inbox.CancellationToken);
            await inbox.Completion;

            // Assert
            Assert.Equal(2, validationCount);
            Assert.Equal(2, inbox.StartTlsRequests);
            Assert.Equal(2, inbox.CompletedMessages.Count);
            var expectedMessageId =
                $"<bdgrz-invitation-{attemptId.ToString().Replace("-", "", StringComparison.Ordinal)}@example.com>";
            foreach (var message in inbox.CompletedMessages)
            {
                Assert.Equal("MAIL FROM:<sender@example.com>", message.Sender);
                Assert.Equal($"RCPT TO:<{recipient}>", message.Recipient);
                Assert.Equal(expectedMessageId, message.Header("Message-ID"));
                Assert.Equal("Your Badgers organization invitation", message.Header("Subject"));
                Assert.Contains($"organization {tenantId}", message.Body, StringComparison.Ordinal);
                Assert.Contains($"Your invitation code is {token}.", message.Body, StringComparison.Ordinal);
            }
        }
        finally
        {
#pragma warning disable SYSLIB0014
            ServicePointManager.ServerCertificateValidationCallback = previousValidation;
#pragma warning restore SYSLIB0014
        }
    }

    [Fact]
    public async Task ShouldRejectUnpinnedCertificateWithoutDeliveringGivenDifferentServerCertificate()
    {
        // Arrange
        await using var inbox = new LoopbackSmtpInbox(connectionCount: 1, expectTlsRejection: true);
        using var pinnedCertificate = LoopbackSmtpInbox.CreateCertificate();
        var settings = new EmailChallengeDeliverySettings("smtp", "127.0.0.1", inbox.Port,
            "sender@example.com", null, null);
        var delivery = new SmtpTenantInvitationDelivery(settings);
        const string recipient = "private-recipient@example.com";
        const string token = "private-invitation-code";
        var rejectedCertificate = false;

#pragma warning disable SYSLIB0014
        var previousValidation = ServicePointManager.ServerCertificateValidationCallback;
        ServicePointManager.ServerCertificateValidationCallback = (_, certificate, _, errors) =>
        {
            var accepted = certificate is not null
                           && errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors
                           && certificate.GetRawCertData().AsSpan().SequenceEqual(pinnedCertificate.RawData);
            rejectedCertificate = !accepted;
            return accepted;
        };
#pragma warning restore SYSLIB0014
        try
        {
            // Act
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await delivery.SendAsync(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                    recipient, token, inbox.CancellationToken));
            await inbox.Completion;

            // Assert
            Assert.True(rejectedCertificate);
            Assert.Equal(1, inbox.StartTlsRequests);
            Assert.Empty(inbox.CompletedMessages);
            Assert.Equal("Invitation delivery failed.", failure.Message);
            Assert.DoesNotContain(recipient, failure.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(token, failure.ToString(), StringComparison.Ordinal);
        }
        finally
        {
#pragma warning disable SYSLIB0014
            ServicePointManager.ServerCertificateValidationCallback = previousValidation;
#pragma warning restore SYSLIB0014
        }
    }

    [Fact]
    public async Task ShouldHideRecipientAndTokenGivenMalformedRecipient()
    {
        // Arrange
        var settings = new EmailChallengeDeliverySettings("smtp", "smtp.example.com", 587,
            "sender@example.com", null, null);
        var delivery = new SmtpTenantInvitationDelivery(settings);
        const string recipient = "not-an-address";
        const string token = "private-invitation-token";

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await delivery.SendAsync(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                recipient, token, CancellationToken.None));

        // Assert
        Assert.Equal("Invitation delivery failed.", failure.Message);
        Assert.DoesNotContain(recipient, failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, failure.ToString(), StringComparison.Ordinal);
    }

    internal sealed record SmtpMessage(string Sender, string Recipient, string Data)
    {
        public string Header(string name) => Data.Split("\r\n", StringSplitOptions.None)
            .TakeWhile(line => line.Length != 0)
            .Single(line => line.StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase))
            [(name.Length + 1)..].Trim();

        public string Body
        {
            get
            {
                var body = Data[(Data.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
                return Header("Content-Transfer-Encoding") switch
                {
                    "base64" => Encoding.ASCII.GetString(Convert.FromBase64String(body)),
                    // The invitation's ASCII body needs only soft-line-break normalization.
                    "quoted-printable" => body.Replace("=\r\n", "", StringComparison.Ordinal),
                    _ => body,
                };
            }
        }
    }

    internal sealed class LoopbackSmtpInbox : IAsyncDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource _lifetime;
        readonly bool _expectTlsRejection;

        public LoopbackSmtpInbox(int connectionCount, bool expectTlsRejection = false,
            TimeSpan? lifetime = null)
        {
            var timeout = lifetime ?? TimeSpan.FromSeconds(15);
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(3))
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            _lifetime = new CancellationTokenSource(timeout);
            _expectTlsRejection = expectTlsRejection;
            Certificate = CreateCertificate();
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Completion = ReceiveAsync(connectionCount);
        }

        public X509Certificate2 Certificate { get; }
        public int Port { get; }
        public int StartTlsRequests { get; private set; }
        public List<SmtpMessage> CompletedMessages { get; } = [];
        public Task Completion { get; }
        public CancellationToken CancellationToken => _lifetime.Token;

        public static X509Certificate2 CreateCertificate()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
        }

        async Task ReceiveAsync(int connectionCount)
        {
            for (var index = 0; index < connectionCount; index++)
            {
                using var client = await _listener.AcceptTcpClientAsync(CancellationToken);
                var network = client.GetStream();
                using var plaintextReader = new StreamReader(network, Encoding.ASCII, false, 1024, leaveOpen: true);
                await using var plaintextWriter = Writer(network);
                await ReplyAsync(plaintextWriter, "220 localhost controlled SMTP inbox");
                Assert.StartsWith("EHLO ", await ReadAsync(plaintextReader), StringComparison.Ordinal);
                await ReplyAsync(plaintextWriter, "250-localhost");
                await ReplyAsync(plaintextWriter, "250 STARTTLS");
                Assert.Equal("STARTTLS", await ReadAsync(plaintextReader));
                StartTlsRequests++;
                await ReplyAsync(plaintextWriter, "220 Ready to start TLS");

                try
                {
                    await using var tls = new SslStream(network, leaveInnerStreamOpen: true);
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = Certificate,
                        EnabledSslProtocols = SslProtocols.None,
                        ClientCertificateRequired = false,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    }, CancellationToken);
                    using var reader = new StreamReader(tls, Encoding.ASCII, false, 1024, leaveOpen: true);
                    await using var writer = Writer(tls);
                    Assert.StartsWith("EHLO ", await ReadAsync(reader), StringComparison.Ordinal);
                    await ReplyAsync(writer, "250 localhost");
                    var sender = await ReadAsync(reader);
                    Assert.StartsWith("MAIL FROM:", sender, StringComparison.Ordinal);
                    await ReplyAsync(writer, "250 Sender accepted");
                    var recipient = await ReadAsync(reader);
                    Assert.StartsWith("RCPT TO:", recipient, StringComparison.Ordinal);
                    await ReplyAsync(writer, "250 Recipient accepted");
                    Assert.Equal("DATA", await ReadAsync(reader));
                    await ReplyAsync(writer, "354 End data with <CRLF>.<CRLF>");
                    var data = new StringBuilder();
                    while (await ReadAsync(reader) is { } line && line != ".")
                        data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line).Append("\r\n");
                    CompletedMessages.Add(new SmtpMessage(sender, recipient, data.ToString()));
                    await ReplyAsync(writer, "250 Message accepted");
                    Assert.Equal("QUIT", await ReadAsync(reader));
                    await ReplyAsync(writer, "221 Goodbye");
                }
                catch (Exception failure) when (_expectTlsRejection
                                                && failure is AuthenticationException or IOException)
                {
                    // A client rejecting our unpinned certificate closes the TLS connection.
                }
            }
        }

        static StreamWriter Writer(Stream stream) => new(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };

        async Task<string> ReadAsync(StreamReader reader) =>
            await reader.ReadLineAsync(CancellationToken) ?? throw new EndOfStreamException("SMTP peer disconnected.");

        Task ReplyAsync(StreamWriter writer, string line) => writer.WriteLineAsync(line.AsMemory(), CancellationToken);

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            _listener.Stop();
            try
            {
                await Completion;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                // Cancel pending accept/read operations if an assertion interrupted the exchange.
            }
            finally
            {
                Certificate.Dispose();
                _lifetime.Dispose();
            }
        }
    }
}
