using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Composes the production readiness owners and process workers over in-memory stores.</summary>
static class ReadinessTestServices
{
    public static ServiceProvider Build(ICriteriaCatalog? catalog = null, IReadinessSourceReader? sources = null,
        IImmutableContentStore? immutableContent = null)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new PersistenceKvClient(new InMemoryKvClient()));
        services.AddScoped<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        services.AddSingleton<IProgramResourceScopeResolver>(ProgramManagementServices.ResourceScopes());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(allowed: true)));
        if (immutableContent is not null)
            services.AddSingleton(immutableContent);
        if (catalog is not null)
            services.AddSingleton(catalog);
        if (sources is not null)
            services.AddSingleton(sources);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
