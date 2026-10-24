using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class ContentSourceFactRegistrationTests
{
    [Fact]
    public void ShouldRegisterStoreAsSingletonAndLeaveResolverUnregisteredGivenApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());

        // Assert
        Assert.Equal(ServiceLifetime.Singleton,
            services.Last(descriptor => descriptor.ServiceType == typeof(IImmutableContentStore)).Lifetime);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISourceFactResolver));
    }

}
