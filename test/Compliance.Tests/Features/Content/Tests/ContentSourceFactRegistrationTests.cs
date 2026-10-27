using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Incidents.Commands;
namespace Bdgrz.Compliance.Tests.Features.Content.Tests;

public sealed class ContentSourceFactRegistrationTests
{
    [Fact]
    public async Task ShouldResolveOneChildMaterializationProcessGivenCompleteApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build());
        await using var provider = services.BuildServiceProvider();

        // Act
        var catalog = provider.GetRequiredService<ProcessCatalog>();

        // Assert
        Assert.Single(catalog.Definitions, definition => definition.Name == "IndependentChildMaterialization");
    }

    [Fact]
    public async Task ShouldRegisterAndResolveStoreAndSourceResolverGivenApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<ISourceFactResolver>();

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal(ServiceLifetime.Singleton,
            services.Last(descriptor => descriptor.ServiceType == typeof(IImmutableContentStore)).Lifetime);
        var resolver = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISourceFactResolver));
        Assert.Equal(ServiceLifetime.Scoped, resolver.Lifetime);
        Assert.NotNull(resolver.ImplementationFactory);
    }

}
