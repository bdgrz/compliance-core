using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

static class VerifiedWorkDigestServices
{
    public static void AddVerifiedWorkDigestReader(this IServiceCollection services)
    {
        services.TryAddSingleton<IImmutableContentStore>(_ => new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            10, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1)));
        services.TryAddScoped<ISourceFactResolver, EventStoreSourceFactResolver>();
        services.TryAddScoped<NativeOperationExecutor>();
        services.TryAddScoped<WorkDigestRetryOperations>();
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
            new WorkDigestAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(readerKey),
                provider.GetRequiredService<IDomainEventReader>(), provider.GetRequiredService<ISourceFactResolver>()), originalReader.Lifetime));
    }
}
