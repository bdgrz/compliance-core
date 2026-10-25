using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

static class VerifiedProductClientServiceServices
{
    public static void AddVerifiedProductClientServiceReader(this IServiceCollection services)
    {
        var originalReader = services.Last(descriptor => !descriptor.IsKeyedService &&
            descriptor.ServiceType == typeof(IAggregateReader));
        var readerKey = new object();
        services.Remove(originalReader);
        if (originalReader.ImplementationInstance is { } instance)
            services.Add(ServiceDescriptor.KeyedSingleton(typeof(IAggregateReader), readerKey, instance));
        else if (originalReader.ImplementationFactory is { } factory)
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), readerKey,
                (provider, _) => factory(provider), originalReader.Lifetime));
        else
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), readerKey,
                originalReader.ImplementationType!, originalReader.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(IAggregateReader), provider =>
            new ProductClientServiceAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(readerKey),
                provider.GetRequiredService<IDomainEventReader>()), originalReader.Lifetime));
    }
}
