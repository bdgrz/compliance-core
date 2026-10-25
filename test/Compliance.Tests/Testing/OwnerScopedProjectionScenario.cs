using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class OwnerScopedProjectionScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryKvClient Kv { get; } = new();
    public InMemoryEventStore Events { get; } = new();
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IImmutableContentStore Content { get; }

    public OwnerScopedProjectionScenario()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Content = new FitzKvImmutableContentStore(Kv);
    }

    public async Task<IReadOnlyList<DomainEvent>> ReadAsync(Uuid tenant, string area)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), area),
            EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                facts.Add(record.Event);
        return facts;
    }

    public static async Task ProjectAsync(IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply,
        CheckpointIdentity identity, IReadOnlyList<DomainEvent> facts)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        foreach (var fact in facts)
            await apply(fact, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
