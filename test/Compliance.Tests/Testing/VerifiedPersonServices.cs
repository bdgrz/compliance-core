using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

static class VerifiedPersonServices
{
    public static void AddPersonNativeFixtures(this IServiceCollection services)
    {
        services.TryAddSingleton<IDomainEventReader>(provider => (IDomainEventReader)provider.GetRequiredService<IEventStore>());
        services.TryAddSingleton<IImmutableContentStore>(_ => new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient())));
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
            new PersonAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(readerKey),
                provider.GetRequiredService<IDomainEventReader>()), originalReader.Lifetime));
    }
}
