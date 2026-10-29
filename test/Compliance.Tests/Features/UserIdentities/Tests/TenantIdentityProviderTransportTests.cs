using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>The tenant identity-provider routes: caller-chosen command identities, exact retries and retired routes.</summary>
[Trait("Category", "WebIntegration")]
public sealed class TenantIdentityProviderTransportTests
{
    const string Reference = "vault://tenant/idp/credential-v1";
    static readonly string[] Audiences = ["client-app"];

    [Fact]
    public async Task ShouldAcceptEveryProviderCommandOnceGivenExactHttpRetries()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var provider = Uuid.CreateVersion4();
        var configure = Uuid.CreateVersion4();
        var rotate = Uuid.CreateVersion4();
        var disable = Uuid.CreateVersion4();
        var root = $"/api/v1/tenants/{tenant}/identity-providers/{provider}";

        // Act
        var configured = await PutTwiceAsync($"{root}/{configure}", new
        {
            issuer = "https://idp.example.test",
            client_id = "client-app",
            encrypted_credential_reference = Reference,
            allowed_audiences = Audiences,
            expected_revision = 0,
        });
        var tested = await PutTwiceAsync($"{root}/tests/oidc-check", new { expected_revision = 1 });
        var rotated = await PutTwiceAsync($"{root}/credential-rotations/{rotate}", new
        {
            encrypted_credential_reference = "vault://tenant/idp/credential-v2",
            rotation_reference = "rotation-1",
            expected_revision = 2,
        });
        var disabled = await PutTwiceAsync($"{root}/disable/{disable}",
            new { expected_revision = 3, rationale = "The client retired this provider." });
        var facts = await FactsAsync(factory, tenant, provider);

        // Assert
        Assert.All(new[] { configured, tested, rotated, disabled }, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Collection(facts,
            fact => Assert.Equal(configure, Assert.IsType<TenantIdentityProviderConfigured>(fact).Operation.LogicalId),
            fact => Assert.Equal(TestTenantIdentityProvider.TestId(provider, "oidc-check"),
                Assert.IsType<TenantIdentityProviderTestRequested>(fact).Operation.LogicalId),
            fact => Assert.Equal(rotate, Assert.IsType<TenantIdentityProviderCredentialsRotated>(fact).Operation.LogicalId),
            fact => Assert.Equal(disable, Assert.IsType<TenantIdentityProviderDisabled>(fact).Operation.LogicalId));

        async Task<HttpStatusCode> PutTwiceAsync(string path, object body)
        {
            using var first = await client.PutAsJsonAsync(path, body);
            using var retry = await client.PutAsJsonAsync(path, body);
            Assert.Equal(first.StatusCode, retry.StatusCode);
            return first.StatusCode;
        }
    }

    [Fact]
    public async Task ShouldConflictAndKeepTheCredentialReferenceOutOfFactsGivenChangedIntentOnTheSameCommandIdentity()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var provider = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenant}/identity-providers/{provider}/{Uuid.CreateVersion4()}";
        var body = new
        {
            issuer = "https://idp.example.test",
            client_id = "client-app",
            encrypted_credential_reference = Reference,
            allowed_audiences = Audiences,
            expected_revision = 0,
        };
        using var first = await client.PutAsJsonAsync(path, body);

        // Act
        using var changed = await client.PutAsJsonAsync(path, new
        {
            issuer = "https://other-idp.example.test",
            client_id = "client-app",
            encrypted_credential_reference = Reference,
            allowed_audiences = Audiences,
            expected_revision = 0,
        });
        var facts = await FactsAsync(factory, tenant, provider);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var configured = Assert.IsType<TenantIdentityProviderConfigured>(Assert.Single(facts));
        Assert.DoesNotContain("vault://", JsonSerializer.Serialize(configured,
            ComplianceCoreJsonContext.Default.TenantIdentityProviderConfigured), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/v1/tenants/{t}/identity-providers/{p}")]
    [InlineData("/api/v1/tenants/{t}/identity-providers/{p}/tests")]
    [InlineData("/api/v1/tenants/{t}/identity-providers/{p}/credential-rotations")]
    [InlineData("/api/v1/tenants/{t}/identity-providers/{p}/disable")]
    public async Task ShouldRetireTheOldProviderRoutesGivenIdentifiedProviderCommands(string template)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var provider = Uuid.CreateVersion4();
        var path = template.Replace("{t}", tenant.ToString()).Replace("{p}", provider.ToString());

        // Act
        using var post = await client.PostAsJsonAsync(path, new { expected_revision = 0 });

        // Assert
        Assert.False(post.IsSuccessStatusCode);
        Assert.Empty(await FactsAsync(factory, tenant, provider));
    }

    static async Task<List<DomainEvent>> FactsAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        Uuid tenant, Uuid provider)
    {
        var facts = new List<DomainEvent>();
        try
        {
            await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new TenantIdentityProvider(tenant, provider).Stream, 0))
                facts.Add(record.Event);
        }
        catch (InvalidOperationException)
        {
            // An unknown stream reads as empty.
        }

        return facts;
    }

    static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new { email_address = "provider-transport@example.com" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
