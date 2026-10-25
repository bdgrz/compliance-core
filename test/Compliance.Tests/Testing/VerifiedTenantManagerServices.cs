using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

static class VerifiedTenantManagerServices
{
    public static void AddVerifiedTenantManagerReader(this IServiceCollection services)
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
            new TenantManagerGuardAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(readerKey),
                provider.GetRequiredService<IDomainEventReader>(), provider.GetRequiredService<Bdgrz.Compliance.Features.Content.IImmutableContentStore>(),
                provider.GetRequiredService<TimeProvider>()), originalReader.Lifetime));
    }
}
