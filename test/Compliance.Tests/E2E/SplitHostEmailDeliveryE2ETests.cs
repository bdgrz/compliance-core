using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SplitHostEmailDeliveryE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldDeliverSameChallengeGivenWorkerRestartAfterUnacknowledgedSend()
    {
        // Arrange
        var applicationName = $"compliance-email-retry-{Guid.NewGuid():N}";
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var firstDelivery = new RecordingDelivery(pauseAfterSend: true);
        var workerLogs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        var apiLogs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var firstWorker = CreateWorker(applicationName, key, firstDelivery, workerLogs);
        await firstWorker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName)
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("Compliance:EmailDelivery:ActiveTokenKeyId", "test-key");
                host.UseSetting("Compliance:EmailDelivery:TokenKeys:test-key", key);
                host.ConfigureLogging(logging => logging.AddProvider(apiLogs));
            });
        var previousMode = TestHostMode.Current;
        HttpClient client;
        try
        {
            TestHostMode.Set("api");
            client = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = client;
        var email = $"restart-{Guid.NewGuid():N}@example.com";
        var userId = await TenantInvitationE2ETests.LoginAsync(owner, email);
        var path = $"/api/v1/users/{userId}/email-addresses/{email}";
        await WaitForStatusAsync(owner, path, "not_issued");

        // Act: the first worker hands the message to the provider, then loses its acknowledgment.
        var challengeId = Guid.NewGuid();
        using var issued = await owner.PutAsync($"{path}/challenges/{challengeId}", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var firstDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < firstDeadline && firstDelivery.Attempts.IsEmpty)
            await Task.Delay(250);
        Assert.False(firstDelivery.Attempts.IsEmpty);
        await firstWorker.StopAsync();
        await WaitForStatusAsync(owner, path, "pending");
        var retryDelivery = new RecordingDelivery(pauseAfterSend: false);
        using var secondWorker = CreateWorker(applicationName, key, retryDelivery, workerLogs);
        await secondWorker.StartAsync();
        try
        {
            await WaitForStatusAsync(owner, path, "delivered");

            // Assert
            var firstAttempt = Assert.Single(firstDelivery.Attempts.Distinct());
            var secondAttempt = Assert.Single(retryDelivery.Attempts.Distinct());
            Assert.Equal(firstAttempt, secondAttempt);
            // The delivery receipt is process state, not a fact of the address stream: the unacknowledged first
            // attempt was reconciled as unknown before the identical message was sent again.
            var record = await secondWorker.Services.GetRequiredService<IEmailChallengeDeliveryLedger>()
                .ReadAsync(Uuid.Parse(challengeId.ToString(), System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
            Assert.NotNull(record);
            Assert.Equal(2, record.Attempts.Count);
            Assert.Equal(EmailChallengeFailureCertainty.Unknown, record.Attempts[0].Outcome);
            Assert.Equal(EmailChallengeDeliveryAttempt.Sent, record.Attempts[1].Outcome);
            await foreach (var fact in secondWorker.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new EmailAddress(email).Stream, 0, CancellationToken.None))
                Assert.DoesNotContain("Delivery", fact.Event.GetType().Name, StringComparison.Ordinal);
            using var denied = await owner.GetAsync(
                $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges/status");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var status = await owner.GetAsync($"{path}/challenges/status");
            var body = await status.Content.ReadAsStringAsync();
            Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(firstAttempt.Token, body, StringComparison.Ordinal);
            using var completed = await owner.PutAsJsonAsync($"{path}/verifications/{challengeId}",
                new { token = firstAttempt.Token });
            Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
            var logText = string.Join('\n', workerLogs.Records.Concat(apiLogs.Records)
                .SelectMany(record => new[] { record.Message }.Concat(
                    record.State.Select(item => $"{item.Key}={item.Value}"))));
            Assert.DoesNotContain(email, logText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(firstAttempt.Token, logText, StringComparison.Ordinal);
            Assert.DoesNotContain(key, logText, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", logText, StringComparison.Ordinal);
        }
        finally
        {
            await secondWorker.StopAsync();
        }
    }

    IHost CreateWorker(string applicationName, string key, RecordingDelivery delivery,
        ILoggerProvider logs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Configuration["Compliance:EmailDelivery:ActiveTokenKeyId"] = "test-key";
        builder.Configuration["Compliance:EmailDelivery:TokenKeys:test-key"] = key;
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        builder.Services.AddSingleton<IEmailChallengeDelivery>(delivery);
        builder.Logging.AddProvider(logs);
        return builder.Build();
    }

    static async Task WaitForStatusAsync(HttpClient client, string path, string expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}/challenges/status");
            if (response.StatusCode == HttpStatusCode.OK &&
                (await response.Content.ReadFromJsonAsync<StatusDocument>())?.DeliveryStatus == expected)
                return;
            await Task.Delay(250);
        }
        Assert.Fail($"Email challenge status did not become {expected}.");
    }

    sealed class RecordingDelivery(bool pauseAfterSend) : IEmailChallengeDelivery
    {
        public ConcurrentQueue<(Uuid ChallengeId, string Token)> Attempts { get; } = new();

        public async ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            Attempts.Enqueue((challengeId, token));
            if (pauseAfterSend)
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
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

    sealed record StatusDocument([property: JsonPropertyName("delivery_status")] string DeliveryStatus);
}
