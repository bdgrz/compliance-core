using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Runs a cleanup reactor against a real event store and the real request bus, so a test seeds native facts,
///     lets the reactor discover its work from them and reads the owners back. No directory exists anywhere in the
///     host, which is how a lagging or empty projection is exercised.
/// </summary>
static class CleanupReactorHost
{
    /// <summary>Drives the reactor from its checkpoint until it has consumed every record, as a worker would.</summary>
    public static async Task RunAsync(ServiceProvider provider, Uuid tenantId,
        Func<IProjectionCheckpointStore, IRequestBus, IDomainEventReader, IAggregateReader, Reactor> create)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
        var reactor = create(checkpoints, services.GetRequiredService<IRequestBus>(), events,
            services.GetRequiredService<IAggregateReader>());
        await new ReactorScenario(new TenantId(tenantId.ToString())).RunAsync(reactor);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);
        var runner = new ReactorRunner(events);
        while (true)
        {
            var next = await runner.RunAsync(reactor, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    /// <summary>Executes an owner operation as the system, as a seeding step.</summary>
    public static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate owner,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            item => AggregateOutcome.CommitOnSuccess(operation(item)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    /// <summary>Hydrates an owner from the store.</summary>
    public static async Task<TAggregate> ReadAsync<TAggregate>(IServiceProvider provider, TAggregate owner)
        where TAggregate : Aggregate
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
    }
}
