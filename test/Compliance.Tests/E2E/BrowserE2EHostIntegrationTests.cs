using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Readers;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

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

        var challengeId = Guid.NewGuid();
        using var issued = await client.PutAsync($"{addressPath}/challenges/{challengeId}", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var token = await BrowserE2EBridgeTests.ConsumeAsync(browserHost.MailboxSocketPath,
            "email_verification", email, session.Id);
        using var verified = await client.PutAsJsonAsync($"{addressPath}/verifications/{challengeId}",
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

        var registrationSlug = $"browser-{Guid.NewGuid():N}"[..24];
        using var registered = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Browser E2E creator organization",
            slug = registrationSlug,
            legal_name = "Browser E2E creator legal entity",
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var tenant = await registered.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);

        var provisioning = await WaitForCompletedProvisioningAsync(client, tenant.TenantId,
            session.Id, tenant.Slug, browserHost);
        Assert.Equal(tenant.TenantId, provisioning.TenantId);
        Assert.Equal("completed", provisioning.Status);

        var slugResolution = await WaitForCreatorSlugResolutionAsync(client, tenant.Slug,
            tenant.TenantId, session.Id, browserHost);
        Assert.Equal(tenant.TenantId, slugResolution.TenantId);
        Assert.Equal(tenant.Slug, slugResolution.CurrentSlug);
        Assert.False(slugResolution.Redirect);

        using var workspaceResponse = await client.GetAsync($"/api/v1/tenants/{tenant.TenantId}/members");
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
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
        HttpClient client, string tenantId, string userId, string slug, BrowserE2EHost browserHost)
    {
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
                            return provisioning;

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
        HttpClient client, string slug, string tenantId, string userId, BrowserE2EHost browserHost)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var lastHttpStatus = 0;
        var reads = 0;
        var statusProgression = new List<int>();
        while (DateTimeOffset.UtcNow < deadline)
        {
            reads++;
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                using var response = await client.GetAsync(
                    $"/api/v1/tenant-slugs/{slug}/mine", requestTimeout.Token);
                lastHttpStatus = (int)response.StatusCode;
                if (statusProgression.Count == 0 || statusProgression[^1] != lastHttpStatus)
                    statusProgression.Add(lastHttpStatus);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var resolution = await response.Content.ReadFromJsonAsync<TenantSlugResolutionDocument>(
                        requestTimeout.Token);
                    if (resolution is not null)
                        return resolution;
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
        var checkpoints = scope.ServiceProvider.GetRequiredService<IProjectionCheckpointStore>();
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), CancellationToken.None);
        var slugAggregate = await reader.HydrateAsync(new TenantSlug(slug), CancellationToken.None);
        var member = await reader.HydrateAsync(new Member(tenantId, userId), CancellationToken.None);
        var memberId = RbacIds.Member(tenantId, userId);
        var administratorAssignment = await reader.HydrateAsync(new TeamMember(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), memberId), CancellationToken.None);
        var administratorsTeam = await reader.HydrateAsync(new Team(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId)), CancellationToken.None);
        var powerUsersTeam = await reader.HydrateAsync(new Team(tenantId,
            BuiltInRbac.PowerUsersTeamId(tenantId)), CancellationToken.None);
        var standardUsersTeam = await reader.HydrateAsync(new Team(tenantId,
            BuiltInRbac.StandardUsersTeamId(tenantId)), CancellationToken.None);
        var administratorRole = await reader.HydrateAsync(new Role(tenantId,
            BuiltInRbac.TenantAdministrationRoleId(tenantId)), CancellationToken.None);
        var complianceManagementRole = await reader.HydrateAsync(new Role(tenantId,
            BuiltInRbac.ComplianceManagementRoleId(tenantId)), CancellationToken.None);
        var participationRole = await reader.HydrateAsync(new Role(tenantId,
            BuiltInRbac.ComplianceParticipationRoleId(tenantId)), CancellationToken.None);
        var administratorTeamRole = await reader.HydrateAsync(new TeamRole(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), BuiltInRbac.TenantAdministrationRoleId(tenantId)),
            CancellationToken.None);
        var powerUsersTeamRole = await reader.HydrateAsync(new TeamRole(tenantId,
            BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.ComplianceManagementRoleId(tenantId)),
            CancellationToken.None);
        var standardUsersTeamRole = await reader.HydrateAsync(new TeamRole(tenantId,
            BuiltInRbac.StandardUsersTeamId(tenantId), BuiltInRbac.ComplianceParticipationRoleId(tenantId)),
            CancellationToken.None);
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, CancellationToken.None);
        var tenantSlugConfirmed = false;
        var tenantActivated = false;
        var creatorIsAdministrator = false;
        var firstAdministratorEmailPresent = false;
        var sourceRecordsRead = 0;
        await foreach (var record in events.ReadAsync(new EventStreamAddress("bdgrz", "tenants",
                           tenantId.ToString()), 0, CancellationToken.None))
        {
            if (record.Event is TenantRegistered registration)
            {
                creatorIsAdministrator = registration.CreatorIsAdministrator;
                firstAdministratorEmailPresent = registration.FirstAdministratorEmail is not null;
            }
            tenantSlugConfirmed |= record.Event is TenantSlugConfirmed;
            tenantActivated |= record.Event is TenantActivated;
            sourceRecordsRead++;
            if (sourceRecordsRead >= 32 || tenantSlugConfirmed && tenantActivated)
                break;
        }

        var bootstrapCheckpointAdvanced = await HasAdvancedAsync(scope.ServiceProvider,
            checkpoints, "TenantRbacBootstrap");

        return new ReadinessSnapshot(
            tenant.IsRegistered,
            creatorIsAdministrator,
            firstAdministratorEmailPresent,
            tenantSlugConfirmed,
            tenantActivated,
            tenant.HasCompletedActivation,
            tenant.IsActive,
            string.Equals(tenant.CurrentSlug, slug, StringComparison.Ordinal),
            slugAggregate.OwningTenantId == tenantId,
            member.IsRegistered,
            member.IsAccessEligibleOn(DateOnly.FromDateTime(DateTime.UtcNow)),
            membership is not null && membership.TenantId == tenantId,
            administratorAssignment.IsAssigned,
            administratorsTeam.IsActive,
            powerUsersTeam.IsActive,
            standardUsersTeam.IsActive,
            administratorRole.IsActive,
            complianceManagementRole.IsActive,
            participationRole.IsActive,
            administratorTeamRole.IsAssigned,
            powerUsersTeamRole.IsAssigned,
            standardUsersTeamRole.IsAssigned,
            bootstrapCheckpointAdvanced);
    }

    static async Task<bool> HasAdvancedAsync(IServiceProvider services,
        IProjectionCheckpointStore checkpoints, string workloadName)
    {
        var registration = services.GetServices<WorkloadRegistration>()
            .Single(workload => workload.Name == workloadName);
        var reactor = (Reactor)services.GetRequiredService(registration.ComponentType);
        var checkpoint = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
        return checkpoint != ProjectionCheckpoint.Start;
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
        bool CreatorIsAdministrator,
        bool FirstAdministratorEmailPresent,
        bool TenantSlugConfirmed,
        bool TenantActivated,
        bool TenantHasCompletedActivation,
        bool TenantActive,
        bool TenantSlugMatches,
        bool SlugOwnerMatches,
        bool MemberRegistered,
        bool MemberEligible,
        bool MembershipProjected,
        bool AdministratorTeamMemberAssigned,
        bool AdministratorsTeamDefined,
        bool PowerUsersTeamDefined,
        bool StandardUsersTeamDefined,
        bool AdministratorRoleDefined,
        bool ComplianceManagementRoleDefined,
        bool ParticipationRoleDefined,
        bool AdministratorRoleAssignedToTeam,
        bool ComplianceManagementRoleAssignedToTeam,
        bool ParticipationRoleAssignedToTeam,
        bool TenantRbacBootstrapCheckpointAdvanced);

}
