using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class ProductClientServiceFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; }
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public ProductClientServiceReader Services => new(Reader, Events, Content);
    public ProductClientServiceOperations Operations { get; }

    public ProductClientServiceFixture(InMemoryEventStore? events = null, IDomainEventReader? verificationEvents = null)
    {
        Events = events ?? new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(verificationEvents ?? Events);
        services.AddPortia();
        services.AddVerifiedProgramReader();
        services.AddVerifiedProductClientServiceReader();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Operations = new ProductClientServiceOperations(new NativeOperationExecutor(Reader, Executor, Content), Reader, Content, TimeProvider.System);
    }

    public async Task SeedProgramAsync(Uuid tenant, Uuid program, Uuid user)
    {
        var rules = new ProgramRules(tenant, program);
        Assert.Null(rules.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            RbacIds.Member(tenant, user), "Program author", DateTimeOffset.UtcNow));
        await ProgramSeeds.WriteAsync(Reader, Writer, Events, Content, rules);
    }

    public Task PublishAsync(ProductClientServiceRules rules) => ProductClientServiceSeeds.AppendAsync(Reader, Writer, Events, Content, rules);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
