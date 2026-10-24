using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

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
        Assert.All(new[] { configured, tested, rotated, disabled }, status => Assert.Equal(HttpStatusCode.NoContent, status));
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
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
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
