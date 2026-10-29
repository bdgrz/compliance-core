using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class EmailChallengeDeliveryConfigurationTests
{
    [Fact]
    public void ShouldRejectMockDeliveryGivenProductionComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = Configuration(null);

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            services.AddCompliance(configuration, requireRealEmailDelivery: true));

        // Assert
        Assert.Contains("must be configured", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRejectMissingKeyGivenProductionSmtpConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = Configuration(null, smtp: true);

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            services.AddCompliance(configuration, requireRealEmailDelivery: true));

        // Assert
        Assert.Contains("active email delivery token key", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRegisterSmtpGivenProductionConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = Configuration(key, smtp: true);

        // Act
        _ = services.AddCompliance(configuration, requireRealEmailDelivery: true);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<SmtpEmailChallengeDelivery>(
            provider.GetRequiredService<IEmailChallengeDelivery>());
        Assert.IsType<SmtpTenantInvitationDelivery>(
            provider.GetRequiredService<ITenantInvitationDelivery>());
    }

    static IConfiguration Configuration(string? key, bool smtp = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
            ["Compliance:EmailDelivery:Mode"] = smtp ? "smtp" : "mock",
            ["Compliance:EmailDelivery:Smtp:Host"] = "smtp.example.com",
            ["Compliance:EmailDelivery:Smtp:Port"] = "587",
            ["Compliance:EmailDelivery:Smtp:FromAddress"] = "verify@example.com",
            ["Compliance:EmailDelivery:ActiveTokenKeyId"] = key is null ? null : "current",
            ["Compliance:EmailDelivery:TokenKeys:current"] = key,
            ["Compliance:WorkDigest:ApplicationOrigin"] = "https://app.example",
            ["Compliance:WorkDigest:ClientWorkItemRouteTemplate"] =
                "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
