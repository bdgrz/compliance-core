using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Authentication.Tests;

/// <summary>ADR 0009: each trusted OIDC issuer is validated directly with its own keys.</summary>
[Trait("Category", "WebIntegration")]
public sealed class MultipleTrustedIssuerWebTests
{
    const string FirmIssuer = "https://firm-issuer.example/";
    const string ClientIssuer = "https://client-issuer.example/";
    const string FirmSecret = "firm-issuer-test-signing-key-00000001";
    const string ClientSecret = "client-issuer-test-signing-key-0000001";

    [Fact]
    public async Task ShouldBindSeparateUsersGivenSameSubjectFromTwoTrustedIssuers()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var firm = await ContinueAsync(client, Token(FirmIssuer, FirmSecret, "shared-subject"));
        using var clientIssuer = await ContinueAsync(client,
            Token(ClientIssuer, ClientSecret, "shared-subject"));
        using var firmAgain = await ContinueAsync(client, Token(FirmIssuer, FirmSecret, "shared-subject"));

        // Assert
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            (firm.StatusCode, clientIssuer.StatusCode, firmAgain.StatusCode));
        var firmUser = await firm.Content.ReadFromJsonAsync<IdentityDocument>();
        var clientUser = await clientIssuer.Content.ReadFromJsonAsync<IdentityDocument>();
        var firmReplay = await firmAgain.Content.ReadFromJsonAsync<IdentityDocument>();
        Assert.NotNull(firmUser);
        Assert.NotNull(clientUser);
        Assert.NotEqual(firmUser.UserId, clientUser.UserId);
        Assert.Equal(firmUser.UserId, firmReplay?.UserId);
    }

    [Fact]
    public async Task ShouldConfigureEachSchemeFromItsOwnResourceGivenTwoTrustedIssuers()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var options = factory.Services.GetRequiredService<
            Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>();
        var firm = options.Get("BdgrzResource0");
        var clientIssuer = options.Get("BdgrzResource1");

        // Assert
        Assert.Equal((FirmIssuer, "compliance-api"), (firm.Authority, firm.Audience));
        Assert.Equal((ClientIssuer, "compliance-api"), (clientIssuer.Authority, clientIssuer.Audience));
        Assert.All([firm, clientIssuer], scheme =>
        {
            Assert.True(scheme.TokenValidationParameters.ValidateIssuer);
            Assert.True(scheme.TokenValidationParameters.ValidateAudience);
            Assert.True(scheme.TokenValidationParameters.ValidateIssuerSigningKey);
            Assert.True(scheme.RequireHttpsMetadata);
            Assert.False(scheme.MapInboundClaims);
        });
    }

    [Fact]
    public async Task ShouldRejectTokenGivenIssuerSignedWithAnotherIssuersKey()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var crossSigned = await ContinueAsync(client, Token(ClientIssuer, FirmSecret, "subject"));
        using var untrusted = await ContinueAsync(client,
            Token("https://untrusted-issuer.example/", FirmSecret, "subject"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, crossSigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, untrusted.StatusCode);
    }

    static async Task<HttpResponseMessage> ContinueAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/oidc-user-sessions")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            ProductionEmailDeliveryTestConfiguration.Apply(builder);
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", FirmIssuer);
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            // Resource schemes are numbered in configuration-key order.
            builder.UseSetting("Compliance:Authentication:Resources:A-Firm:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:Resources:B-Client:Authority", ClientIssuer);
            builder.UseSetting("Compliance:Authentication:Resources:B-Client:Audience", "compliance-api");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-multi-issuer-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(item =>
                             item.ServiceType == typeof(IHostedService)).ToArray())
                    services.Remove(descriptor);
                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
                TrustIssuer(services, "BdgrzResource0", FirmSecret);
                TrustIssuer(services, "BdgrzResource1", ClientSecret);
            });
        });

    static void TrustIssuer(IServiceCollection services, string scheme, string secret) =>
        services.PostConfigure<JwtBearerOptions>(scheme, options =>
        {
            // Replaces only metadata discovery. The expected issuer comes from the scheme's
            // configured Authority and the audience from its configured Audience.
            var metadata = new OpenIdConnectConfiguration { Issuer = options.Authority };
            metadata.SigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)));
            options.Configuration = metadata;
            options.ConfigurationManager =
                new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(
                    metadata);
        });

    static string Token(string issuer, string secret, string subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, "compliance-api",
            [new Claim(JwtRegisteredClaimNames.Sub, subject)], now.AddMinutes(-1),
            now.AddMinutes(30), new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    sealed record IdentityDocument(
        [property: JsonPropertyName("user_identity_id")] string UserIdentityId,
        [property: JsonPropertyName("user_id")] string UserId);
}
