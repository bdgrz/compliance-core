using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
namespace Bdgrz.Compliance.Tests.Testing;

sealed class ProgramNativeFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; }
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public ProgramReader Programs => new(Reader, Events, Content);
    public ProgramOperations Operations { get; }

    public ProgramNativeFixture(InMemoryEventStore? events = null)
    {
        Events = events ?? new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddSingleton<IDomainEventReader>(Events);
        services.AddPortia();
        services.AddVerifiedProgramReader();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Operations = new ProgramOperations(new NativeOperationExecutor(Reader, Executor, Content), Content, TimeProvider.System);
    }

    public Task PublishAsync(ProgramRules rules) => ProgramSeeds.AppendAsync(Reader, Writer, Events, Content, rules);

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
