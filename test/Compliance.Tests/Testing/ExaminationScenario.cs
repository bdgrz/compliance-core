using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

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
