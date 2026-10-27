using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
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
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Broker proof for #430: one tenant's activation held beyond the former 20-pass failure
///     limit neither stops the split worker host nor delays another tenant, and it recovers after
///     the prerequisite is released and after a worker restart.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class TenantActivationIsolationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    const int FormerFailedPassLimit = 20;

    [Fact]
    public async Task ShouldKeepWorkerAndOtherTenantRunningGivenActivationHeldBeyondFailedPassLimit()
    {
        // Arrange
        var applicationName = $"compliance-activation-isolation-{Guid.NewGuid():N}";
        await using var factory = E2EAppFactory.Create(broker, applicationName)
            .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                services.AddSingleton(new PlatformOperatorAuthority([Uuid.CreateVersion4()]))));
        using var heldCreator = ApiClient(factory);
        using var otherCreator = ApiClient(factory);
        var heldEmail = $"held-{Guid.NewGuid():N}@example.com";
        var otherEmail = $"other-{Guid.NewGuid():N}@example.com";
        string heldUserId;
        using (var verificationWorker = BuildWorker(applicationName))
        {
            await verificationWorker.StartAsync();
            var delivery = verificationWorker.Services.GetRequiredService<MockEmailChallengeDelivery>();
            heldUserId = await TenantInvitationE2ETests.LoginAsync(heldCreator, heldEmail);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, heldCreator, heldUserId, heldEmail,
                delivery);
            var otherUserId = await TenantInvitationE2ETests.LoginAsync(otherCreator, otherEmail);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, otherCreator, otherUserId,
                otherEmail, delivery);
            await verificationWorker.StopAsync();
        }

        var lag = new TransientTenantAccessPermissionLag();
        lag.TargetUser(heldUserId);
        using var logs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var worker = BuildWorker(applicationName, lag, logs);
        await worker.StartAsync();
        var lifetime = worker.Services.GetRequiredService<IHostApplicationLifetime>();
        var heldTenantId = await RegisterAsync(heldCreator, "Held Activation");
        int DeferredPasses() => logs.Records.Count(record =>
            record.EventName == "LogActivationDeferred" &&
            string.Equals(record.Value("TenantId")?.ToString(), heldTenantId.ToString(),
                StringComparison.Ordinal));

        // Act
        var otherTenantId = await RegisterAsync(otherCreator, "Unaffected Tenant");
        var otherStatus = await WaitForStatusAsync(otherCreator, otherTenantId, "active");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DeferredPasses() <= FormerFailedPassLimit && !lifetime.ApplicationStopping.IsCancellationRequested &&
               DateTimeOffset.UtcNow < deadline)
            await Task.Delay(250);
        var heldPasses = DeferredPasses();
        var stoppingWhileHeld = lifetime.ApplicationStopping.IsCancellationRequested;
        await using var checkpointScope = worker.Services.CreateAsyncScope();
        var checkpointStore = checkpointScope.ServiceProvider.GetRequiredService<IProjectionCheckpointStore>();
        var activationCheckpoint = new CheckpointIdentity("TenantSelfServiceActivationV1",
            EventStreamPattern.ForPattern(heldTenantId.ToString(), "rbac-team-members"));
        var checkpointWhileHeld = await checkpointStore.LoadAsync(activationCheckpoint);
        using var heldView = await heldCreator.GetAsync($"/api/v1/tenants/{heldTenantId}");
        var workloadFaults = logs.Records.Count(record =>
            record.ExceptionText?.Contains(nameof(WorkloadFailureException), StringComparison.Ordinal) == true);
        lag.Release();
        var recovered = await WaitForStatusAsync(heldCreator, heldTenantId, "active");
        var checkpointAfterRelease = await checkpointStore.LoadAsync(activationCheckpoint);
        var checkpointDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (checkpointAfterRelease == checkpointWhileHeld && DateTimeOffset.UtcNow < checkpointDeadline)
        {
            await Task.Delay(250);
            checkpointAfterRelease = await checkpointStore.LoadAsync(activationCheckpoint);
        }
        await worker.StopAsync();
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        string? afterRestart;
        try
        {
            afterRestart = await WaitForStatusAsync(heldCreator, heldTenantId, "active");
        }
        finally
        {
            await restarted.StopAsync();
        }

        // Assert
        Assert.Equal("active", otherStatus);
        Assert.True(heldPasses > FormerFailedPassLimit,
            $"The activation was held for only {heldPasses} failed passes.");
        Assert.False(stoppingWhileHeld, "A held tenant activation must not stop the worker host.");
        Assert.Equal(0, workloadFaults);
        Assert.True(lag.FailureCount > FormerFailedPassLimit);
        Assert.NotEqual(HttpStatusCode.OK, heldView.StatusCode);
        Assert.Equal("active", recovered);
        Assert.NotEqual(checkpointWhileHeld, checkpointAfterRelease);
        Assert.Equal("active", afterRestart);
    }

    static HttpClient ApiClient(WebApplicationFactory<Program> factory)
    {
        var priorMode = TestHostMode.Current;
        try
        {
            TestHostMode.Set("api");
            return factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(priorMode);
        }
    }

    static async Task<Uuid> RegisterAsync(HttpClient creator, string name)
    {
        using var registered = await creator.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"isolated-{Guid.NewGuid():N}"[..24],
            legal_name = $"{name} LLC",
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var registration = await registered.Content.ReadFromJsonAsync<Registration>();
        Assert.NotNull(registration);
        return Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
    }

    static async Task<string?> WaitForStatusAsync(HttpClient client, Uuid tenantId, string status)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? current = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                current = (await response.Content.ReadFromJsonAsync<Tenant>())?.Status;
                if (current == status)
                    return current;
            }
            await Task.Delay(250);
        }
        return current;
    }

    IHost BuildWorker(string applicationName, TransientTenantAccessPermissionLag? lag = null,
        ILoggerProvider? logs = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        if (logs is not null)
            builder.Logging.AddProvider(logs);
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        // A short local window lets the held activation exceed the former pass limit in the wait.
        builder.Services.AddSingleton(new TenantActivationPolicy(TimeSpan.FromMilliseconds(100)));
        if (lag is not null)
            TransientTenantAccessPermissionTestRegistration.Install(builder.Services, lag);
        return builder.Build();
    }

    sealed record Registration([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record Tenant(string Status);
}
