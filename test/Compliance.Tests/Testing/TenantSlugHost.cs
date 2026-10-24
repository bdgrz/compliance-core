using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Composes the tenant slug choreography over an in-memory event store: the real slug and tenant handlers on the
///     real request bus and the two real reactors that carry claims to the slug owner and answers back to the tenant.
///     A test seeds the tenant, drives the reactors a record at a time and can rewind a checkpoint to replay a
///     processed source fact, which is how a crash between a reaction and its checkpoint is exercised.
/// </summary>
sealed class TenantSlugHost : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;

    public TenantSlugHost()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        services.AddScoped<NativeOperationExecutor>();
        services.AddPortia()
            .AddRequestHandler<RegisterTenantSlugHandler>()
            .AddRequestHandler<SurrenderTenantSlugHandler>()
            .AddRequestHandler<ConfirmTenantSlugHandler>()
            .AddRequestHandler<RejectTenantSlugHandler>()
            .AddRequestHandler<RequestTenantSlugSurrenderHandler>()
            .AddRequestHandler<ConfirmTenantSlugSurrenderHandler>()
            .AddRequestHandler<RejectTenantSlugSurrenderHandler>()
            .AddRequestAuthorizer<TenantLifecycleReactionAuthorizer>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        Checkpoints = _scope.ServiceProvider.GetRequiredService<IProjectionCheckpointStore>();
        Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Registration = new TenantRegistrationReactor(Checkpoints, Bus);
        Slugs = new TenantSlugReactor(Checkpoints, Bus);
    }

    public IDomainEventReader Events { get; }
    public IProjectionCheckpointStore Checkpoints { get; }
    public IRequestBus Bus { get; }
    public IAggregateReader Reader { get; }

    /// <summary>Starts claims and answers: reacts to tenant facts.</summary>
    public Reactor Registration { get; }

    /// <summary>Carries slug owner answers back to the tenant.</summary>
    public Reactor Slugs { get; }

    public async Task SeedAsync<T>(T owner, Func<T, Result> operation) where T : Aggregate
    {
        var result = await _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            current => AggregateOutcome.CommitOnSuccess(operation(current)),
            new RequestDispatchContext(RequestActor.System));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    /// <summary>Consumes every record the reactor has not yet checkpointed.</summary>
    public async Task RunAsync(Reactor reactor)
    {
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var checkpoint = await Checkpoints.LoadAsync(identity);
        var runner = new ReactorRunner(Events);
        while (true)
        {
            var next = await runner.RunAsync(reactor, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    /// <summary>Forgets the reactor's progress, so every source fact is delivered again.</summary>
    public Task RewindAsync(Reactor reactor) => Checkpoints.SaveAsync(
        new CheckpointIdentity(reactor.Name, reactor.Pattern), ProjectionCheckpoint.Start).AsTask();

    public Task<ProjectionCheckpoint> CheckpointAsync(Reactor reactor) => Checkpoints.LoadAsync(
        new CheckpointIdentity(reactor.Name, reactor.Pattern)).AsTask();

    public async Task<List<DomainEvent>> FactsAsync(string realm, string area)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(realm, area), EventCursor.Start,
                           CancellationToken.None))
            facts.Add(record.Event);
        return facts;
    }

    public ValueTask<Tenant> TenantAsync(Uuid tenantId) => Reader.HydrateAsync(new Tenant(tenantId));

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
