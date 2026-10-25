using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class IndependenceCatalogSourceFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public IndependenceCatalogSourceFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
    }
    public InMemoryEventStore Events { get; } = new();
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public IndependenceRuleCatalogReader Rules => new(Reader, Content);
    public IndependenceRuleRatificationReader Ratifications => new(Reader, Content);
    public IndependenceRuleCatalogOperations Operations(IImmutableContentStore? content = null) => new(
        new NativeOperationExecutor(Reader, Executor, content ?? Content), new(Reader, content ?? Content), content ?? Content, TimeProvider.System);
    public Task SeedAsync(IndependenceRuleCatalogRules rules) => IndependenceCatalogSeeds.AppendAsync(Reader, Writer, Content, rules);
    public Task SeedAsync(IndependenceRuleRatificationRules rules) => IndependenceCatalogSeeds.AppendAsync(Reader, Writer, Content, rules);
    public static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user, string display = "Operator") where T : IRequestBase => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()),
            new Claim("name", display)], "UnitTest")), new HttpInvocation("PUT", "/rules", "/rules", "native-catalog-test")));
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(EventStreamAddress stream)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(stream.Realm, stream.Area, stream.Resource), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result;
    }
    public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
}
