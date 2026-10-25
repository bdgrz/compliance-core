using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class RiskDraftSourceFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public RiskDraftSourceFixture()
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
    public RiskDraftReader Drafts => new(Reader, Events, Content);
    public RiskDraftOperations Operations(IAggregateReader? reader = null, IImmutableContentStore? content = null) => new(
        new NativeOperationExecutor(reader ?? Reader, new AggregateExecutor(reader ?? Reader, Writer), content ?? Content),
        new RiskDraftReader(reader ?? Reader, Events, content ?? Content), reader ?? Reader, content ?? Content, TimeProvider.System);
    public async Task SeedProgramAsync(Uuid tenant, Uuid program, Uuid actor)
    {
        var source = new ComplianceProgram(tenant, program);
        Assert.Null(source.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), actor, "Program author", DateTimeOffset.UtcNow));
        await Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System));
    }
    public async Task<IReadOnlyList<DomainEvent>> PublishAsync(RiskDraftRules rules)
    {
        var before = (await FactsAsync(rules.TenantId, rules.Id)).Count;
        await RiskDraftSeeds.AppendAsync(Reader, Writer, Content, rules);
        return (await FactsAsync(rules.TenantId, rules.Id)).Skip(before).ToArray();
    }
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid tenant, Uuid risk)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area, risk.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result;
    }
    public static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user, string display = "Author") where T : IRequestBase => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()),
            new Claim("name", display)], "UnitTest")), new HttpInvocation("PUT", "/risk", "/risk", "risk-test")));
    public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
}
