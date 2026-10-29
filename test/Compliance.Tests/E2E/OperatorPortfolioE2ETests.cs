using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class OperatorPortfolioE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldListPlatformTenantMetadataGivenConfiguredOperatorAndHostMode(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-operator-portfolio-{Guid.NewGuid():N}";
        // Both theory cases share the class broker and its fixed platform roster stream.
        const string operatorEmail = "operator-portfolio-e2e@example.com";
        const string ordinaryEmail = "ordinary-portfolio-e2e@example.com";
        IHost? worker = null;
        if (splitHosts)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = "Development",
            });
            builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
            builder.Configuration["Fitz:ApplicationName"] = applicationName;
            builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
            builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
            worker = builder.Build();
            await worker.StartAsync();
        }

        try
        {
            Uuid operatorId;
            Uuid suspendedTenantId;
            Uuid activeTenantId;
            await using (var seedFactory = E2EAppFactory.Create(broker, applicationName))
            {
                using var seedClient = CreateClient(seedFactory, splitHosts);
                operatorId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(seedClient,
                    operatorEmail), CultureInfo.InvariantCulture);
                suspendedTenantId = await RegisterAsync(seedClient, "Suspended");
                using var ordinarySeedClient = CreateClient(seedFactory, splitHosts);
                await TenantInvitationE2ETests.LoginAsync(ordinarySeedClient, ordinaryEmail);
                activeTenantId = await RegisterAsync(ordinarySeedClient, "Active");
                await WaitForActiveAsync(seedClient, suspendedTenantId);
                using var suspended = await seedClient.PostAsync(
                    $"/api/v1/tenants/{suspendedTenantId}/suspensions", null);
                Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            }

            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton(new PlatformOperatorAuthority([operatorId]))));
            using var operatorClient = CreateClient(factory, splitHosts);
            using var ordinaryClient = CreateClient(factory, splitHosts);
            Assert.Equal(operatorId, Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                operatorClient, operatorEmail), CultureInfo.InvariantCulture));
            var ordinaryId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                ordinaryClient, ordinaryEmail), CultureInfo.InvariantCulture);
            if (worker is not null)
            {
                await using var scope = worker.Services.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
                var seeded = false;
                var seedDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
                while (DateTimeOffset.UtcNow < seedDeadline)
                {
                    var roster = await reader.HydrateAsync(new PlatformOperatorRoster());
                    if ((await PlatformOperatorRosterContent.ReadAsync(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), roster)).Operators.Any(item => item.UserId == operatorId))
                    {
                        seeded = true;
                        break;
                    }
                    await Task.Delay(100);
                }
                if (!seeded)
                {
                    await using var apiScope = factory.Services.CreateAsyncScope();
                    var apiRoster = await apiScope.ServiceProvider.GetRequiredService<IAggregateReader>()
                        .HydrateAsync(new PlatformOperatorRoster());
                    var apiAuthority = await PlatformOperatorRosterContent.ReadAsync(apiScope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), apiRoster);
                    Assert.Fail($"The worker did not read the operator seed; API initialized={apiRoster.IsInitialized}, " +
                                $"API operator={apiAuthority.Operators.Any(item => item.UserId == operatorId)}.");
                }
            }
            const string path = "/api/v1/platform/tenants";

            // Act
            var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
            var observed = new Dictionary<Uuid, string>();
            while (DateTimeOffset.UtcNow < deadline)
            {
                observed = await ReadAllAsync(operatorClient, path);
                if (observed.GetValueOrDefault(suspendedTenantId) == "suspended" &&
                    observed.GetValueOrDefault(activeTenantId) == "active")
                    break;
                await Task.Delay(250);
            }

            // Assert
            Assert.True(observed.GetValueOrDefault(suspendedTenantId) == "suspended",
                $"Expected suspended tenant {suspendedTenantId}; observed {string.Join(", ", observed)}");
            Assert.True(observed.GetValueOrDefault(activeTenantId) == "active",
                $"Expected active tenant {activeTenantId}; observed {string.Join(", ", observed)}");
            using var firstPage = await operatorClient.GetAsync($"{path}?limit=1");
            using var firstDocument = JsonDocument.Parse(await firstPage.Content.ReadAsStreamAsync());
            var cursor = firstDocument.RootElement.GetProperty("next_cursor").GetString();
            Assert.NotNull(cursor);
            Assert.Single(firstDocument.RootElement.GetProperty("items").EnumerateArray());
            using var secondPage = await operatorClient.GetAsync(
                $"{path}?limit=1&cursor={Uri.EscapeDataString(cursor)}");
            using var secondDocument = JsonDocument.Parse(await secondPage.Content.ReadAsStreamAsync());
            Assert.Single(secondDocument.RootElement.GetProperty("items").EnumerateArray());
            Assert.NotEqual(
                firstDocument.RootElement.GetProperty("items")[0].GetProperty("tenant_id").GetString(),
                secondDocument.RootElement.GetProperty("items")[0].GetProperty("tenant_id").GetString());

            foreach (var query in new[] { "?limit=0", "?limit=201", "?cursor=not-a-cursor" })
            {
                using var invalid = await operatorClient.GetAsync($"{path}{query}");
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }

            using var denied = await ordinaryClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var unauthenticated = factory.CreateClient();
            using var unauthorized = await unauthenticated.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            using var noClientRecordAccess = await operatorClient.GetAsync(
                $"/api/v1/tenants/{activeTenantId}/programs");
            Assert.Equal(HttpStatusCode.NotFound, noClientRecordAccess.StatusCode);

            await using var operatorMcp = await McpScenario.ConnectAsync(operatorClient,
                new Uri(operatorClient.BaseAddress!, "/mcp"));
            _ = await operatorMcp.When("bdgrz.platform.tenant.list",
                new Dictionary<string, object?> { ["limit"] = 2 }).ExpectSuccess();
            foreach (var invalidInput in new[]
                     {
                         new Dictionary<string, object?> { ["limit"] = 0 },
                         new Dictionary<string, object?> { ["limit"] = 201 },
                         new Dictionary<string, object?> { ["cursor"] = "not-a-cursor" },
                     })
                _ = await operatorMcp.When("bdgrz.platform.tenant.list", invalidInput)
                    .ExpectFailure("Validation");
            await using var ordinaryMcp = await McpScenario.ConnectAsync(ordinaryClient,
                new Uri(ordinaryClient.BaseAddress!, "/mcp"));
            _ = await ordinaryMcp.When("bdgrz.platform.tenant.list",
                new Dictionary<string, object?>()).ExpectFailure();

            var initialRoster = await ReadRosterAsync(operatorClient);
            using var deniedGrant = await ordinaryClient.PutAsJsonAsync(
                $"/api/v1/platform/operator-grants/{Guid.NewGuid()}", new
                {
                    user_id = operatorId.ToString(),
                    reason = "Attempt without platform access",
                    expected_revision = initialRoster.GetProperty("revision").GetUInt64(),
                });
            Assert.Equal(HttpStatusCode.Forbidden, deniedGrant.StatusCode);
            _ = await ordinaryMcp.When("bdgrz.platform.operator.list",
                new Dictionary<string, object?>()).ExpectFailure();
            using var unknownUserGrant = await operatorClient.PutAsJsonAsync(
                $"/api/v1/platform/operator-grants/{Guid.NewGuid()}", new
                {
                    user_id = Uuid.CreateVersion4().ToString(),
                    reason = "Unknown user must not become the last operator",
                    expected_revision = initialRoster.GetProperty("revision").GetUInt64(),
                });
            Assert.Equal(HttpStatusCode.Conflict, unknownUserGrant.StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<IPlatformUserDirectoryReader>();
                var userDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
                while (DateTimeOffset.UtcNow < userDeadline &&
                       !await users.ExistsAsync(ordinaryId))
                    await Task.Delay(100);
                Assert.True(await users.ExistsAsync(ordinaryId));
            }

            _ = await operatorMcp.When("bdgrz.platform.operator.grant",
                new Dictionary<string, object?>
                {
                    ["user_id"] = ordinaryId.ToString(),
                    ["reason"] = "Rotate platform operations",
                    ["command_id"] = Guid.NewGuid().ToString(),
                    ["expected_revision"] = initialRoster.GetProperty("revision").GetUInt64(),
                }).ExpectSuccess();
            using var grantedRoster = await ordinaryClient.GetAsync("/api/v1/platform/operators");
            Assert.Equal(HttpStatusCode.OK, grantedRoster.StatusCode);
            using var grantedDocument = JsonDocument.Parse(await grantedRoster.Content.ReadAsStreamAsync());
            Assert.Contains(grantedDocument.RootElement.GetProperty("operators").EnumerateArray(),
                item => item.GetProperty("user_id").GetString() == ordinaryId.ToString());
            if (worker is not null)
            {
                await using var scope = worker.Services.CreateAsyncScope();
                var roster = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new PlatformOperatorRoster());
                var authority = await PlatformOperatorRosterContent.ReadAsync(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), roster);
                Assert.Contains(authority.Operators, item => item.UserId == ordinaryId);
            }
            _ = await ordinaryMcp.When("bdgrz.platform.operator.list",
                new Dictionary<string, object?>()).ExpectSuccess();

            _ = await ordinaryMcp.When("bdgrz.platform.operator.revoke",
                new Dictionary<string, object?>
                {
                    ["user_id"] = operatorId.ToString(),
                    ["reason"] = "Transfer platform operations",
                    ["command_id"] = Guid.NewGuid().ToString(),
                    ["expected_revision"] = grantedDocument.RootElement.GetProperty("revision").GetUInt64(),
                    ["assignment_id"] = grantedDocument.RootElement.GetProperty("operators").EnumerateArray()
                        .Single(item => item.GetProperty("user_id").GetString() == operatorId.ToString())
                        .GetProperty("assignment_id").GetString(),
                }).ExpectSuccess();
            using var revokedAccess = await operatorClient.GetAsync("/api/v1/platform/operators");
            Assert.Equal(HttpStatusCode.Forbidden, revokedAccess.StatusCode);
            _ = await operatorMcp.When("bdgrz.platform.operator.list",
                new Dictionary<string, object?>()).ExpectFailure();
            var soleRoster = await ReadRosterAsync(ordinaryClient);
            using var lastOperator = await ordinaryClient.PutAsJsonAsync(
                $"/api/v1/platform/operator-revocations/{Guid.NewGuid()}", new
                {
                    user_id = ordinaryId.ToString(),
                    reason = "Would leave no operator",
                    expected_revision = soleRoster.GetProperty("revision").GetUInt64(),
                    assignment_id = soleRoster.GetProperty("operators")[0].GetProperty("assignment_id").GetString(),
                });
            Assert.Equal(HttpStatusCode.Conflict, lastOperator.StatusCode);

            await using var restartedApi = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton(new PlatformOperatorAuthority([operatorId]))));
            using var restartedOperator = CreateClient(restartedApi, splitHosts);
            await TenantInvitationE2ETests.LoginAsync(restartedOperator, operatorEmail);
            using var stillRevoked = await restartedOperator.GetAsync("/api/v1/platform/operators");
            Assert.Equal(HttpStatusCode.Forbidden, stillRevoked.StatusCode);

            _ = await ordinaryMcp.When("bdgrz.platform.operator.grant",
                new Dictionary<string, object?>
                {
                    ["user_id"] = operatorId.ToString(),
                    ["reason"] = "Complete roster rotation test",
                    ["command_id"] = Guid.NewGuid().ToString(),
                    ["expected_revision"] = soleRoster.GetProperty("revision").GetUInt64(),
                }).ExpectSuccess();
            var restoredRoster = await ReadRosterAsync(operatorClient);
            _ = await operatorMcp.When("bdgrz.platform.operator.revoke",
                new Dictionary<string, object?>
                {
                    ["user_id"] = ordinaryId.ToString(),
                    ["reason"] = "Restore initial operator for the next host mode",
                    ["command_id"] = Guid.NewGuid().ToString(),
                    ["expected_revision"] = restoredRoster.GetProperty("revision").GetUInt64(),
                    ["assignment_id"] = restoredRoster.GetProperty("operators").EnumerateArray()
                        .Single(item => item.GetProperty("user_id").GetString() == ordinaryId.ToString())
                        .GetProperty("assignment_id").GetString(),
                }).ExpectSuccess();
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    static async Task<JsonElement> ReadRosterAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/platform/operators");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    static HttpClient CreateClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        bool splitHosts)
    {
        var priorMode = TestHostMode.Current;
        try
        {
            if (splitHosts)
                TestHostMode.Set("api");
            return factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(priorMode);
        }
    }

    static async Task<Uuid> RegisterAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"portfolio-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return Uuid.Parse(document.RootElement.GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task WaitForActiveAsync(HttpClient client, Uuid tenantId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                if (document.RootElement.GetProperty("status").GetString() == "active")
                    return;
            }
            await Task.Delay(250);
        }
        Assert.Fail("The tenant did not become active before suspension.");
    }

    static async Task<Dictionary<Uuid, string>> ReadAllAsync(HttpClient client, string path)
    {
        var observed = new Dictionary<Uuid, string>();
        string? cursor = null;
        do
        {
            var query = $"{path}?limit=2";
            if (cursor is not null)
                query += $"&cursor={Uri.EscapeDataString(cursor)}";
            using var response = await client.GetAsync(query);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Portfolio returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
                observed[Uuid.Parse(item.GetProperty("tenant_id").GetString()!, CultureInfo.InvariantCulture)] =
                    item.GetProperty("status").GetString()!;
            cursor = document.RootElement.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return observed;
    }
}
