using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Real native engagement fixture and exact immutable projection fault injection.</summary>
sealed class ExaminationScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; }
    public IImmutableContentStore Content { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public ExaminationEngagementReader Examinations => new(Reader, Events, Content);
    public ExaminationScenario(InMemoryEventStore? events = null, IImmutableContentStore? content = null)
    {
        Events = events ?? new InMemoryEventStore();
        Content = content ?? new FitzKvImmutableContentStore(new InMemoryKvClient());
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddPortia();
        ExaminationSeeds.Configure(services, Events);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
    }
    public Task SaveAsync(ExaminationEngagementRules rules) =>
        ExaminationSeeds.WriteAsync(Reader, Writer, Events, Content, rules);
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(ExaminationEngagementRules rules)
    {
        var view = rules.Current!;
        return (await Examinations.ReadAsync(view.TenantId, view.ProgramId, rules.Id)).History.Facts;
    }
    public static async Task<DomainEvent> ReplaceStateAsync(IImmutableContentStore content,
        DomainEvent source, ExaminationEngagementState replacement)
    {
        var original = (IExaminationEngagementFact)source;
        var change = await content.WriteJsonAsync(original.TenantId.ToString(), Uuid.CreateVersion4(),
            ExaminationEngagementState.Schema, replacement);
        DomainEvent altered = source switch
        {
            ExaminationEngagementOpened opened => opened with { Change = change },
            ExaminationEngagementRevised revised => revised with { Change = change },
            TypeIBaselineFrozen frozen => frozen with { Change = change },
            TypeIBaselineAmended amended => amended with { Change = change },
            _ => throw new InvalidOperationException("Unsupported engagement fault source.")
        };
        altered.AttachMetadata(source.Metadata);
        return altered;
    }
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
