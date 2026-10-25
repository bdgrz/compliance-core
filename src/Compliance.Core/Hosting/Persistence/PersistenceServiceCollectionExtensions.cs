using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Hosting.Persistence;

static class PersistenceServiceCollectionExtensions
{
    public static void AddPersistenceSafety(this IServiceCollection services)
    {
        Decorate<IEventStore>(services, (provider, inner) =>
            new PersistenceEventStore(inner, provider.GetRequiredService<IDomainEventSerializer>()));
        Decorate<IKvClient>(services, (_, inner) => new PersistenceKvClient(inner));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new TypeIICalendarAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IRequestBus>(services, (_, inner) => new PersistenceValidationRequestBus(inner));
    }

    // Keep the original registration keyed so DI still owns its lifetime/disposal, including custom stores.
    static void Decorate<T>(IServiceCollection services, Func<IServiceProvider, T, T> wrap) where T : class
    {
        var original = services.Last(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(T));
        var key = new object();
        services.Remove(original);
        if (original.ImplementationInstance is { } instance)
            services.Add(ServiceDescriptor.KeyedSingleton(typeof(T), key, instance));
        else if (original.ImplementationFactory is { } factory)
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(T), key, (provider, _) => factory(provider), original.Lifetime));
        else
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(T), key,
                original.ImplementationType ?? throw new InvalidOperationException("A persistence service requires an implementation."),
                original.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(T),
            provider => wrap(provider, provider.GetRequiredKeyedService<T>(key)), original.Lifetime));
    }
}
