using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.SourceUse.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
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
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new SourceUseAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new MappingLineAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new AdvisorAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new AuditorRequestAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new AuditPackageManifestAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new AccessReviewAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new EvaluationAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ExaminationEngagementAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new PolicyCampaignAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ApplicationImportSourceAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ApplicationImportBatchAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new FirmStaffAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ProviderGapAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ProfessionalDutyAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new WorkDigestAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>(),
                provider.GetRequiredService<ISourceFactResolver>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new PracticeCommitmentAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ClientServiceAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ServiceEngagementAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new IdentityOwnerAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new WorkAssignmentAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new EvidenceRedactionAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>(),
                provider.GetRequiredService<IImmutableContentStore>(), provider.GetRequiredService<ISourceFactResolver>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ProgramAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new TenantManagerGuardAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>(),
                provider.GetRequiredService<IImmutableContentStore>(), provider.GetRequiredService<TimeProvider>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new WorkPreferenceAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new AuditPopulationAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>(),
                provider.GetRequiredService<IImmutableContentStore>(), provider.GetRequiredService<ISourceFactResolver>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new ProductClientServiceAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
        Decorate<IAggregateReader>(services, (provider, inner) =>
            new Bdgrz.Compliance.Features.Workforce.Readers.PersonAggregateReader(inner, provider.GetRequiredService<IDomainEventReader>()));
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
