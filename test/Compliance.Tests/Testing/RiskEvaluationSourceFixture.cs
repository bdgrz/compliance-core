using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
namespace Bdgrz.Compliance.Tests.Testing;

sealed class RiskEvaluationSourceFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; } = new();
    public InMemoryKvClient Kv { get; } = new();
    public IImmutableContentStore Content { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public FixtureClock Clock { get; } = new();
    public RiskEvaluationReader Evaluations { get; }
    public RiskEvaluationSourceFixture()
    {
        Content = new FitzKvImmutableContentStore(Kv);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Evaluations = new(Reader, Events, Content);
    }
    public RiskEvaluationOperations Operations(IAggregateReader? reader = null, IImmutableContentStore? content = null) => new(reader ?? Reader,
        new RiskEvaluationReader(reader ?? Reader, Events, content ?? Content), new NativeOperationExecutor(reader ?? Reader, Executor, content ?? Content),
        content ?? Content, Clock, new RiskMethodReader(reader ?? Reader, Events, content ?? Content));
    public ValueTask<RiskEvaluation> SeedAsync(RiskEvaluationRules rules) => RiskEvaluationSeeds.AppendAsync(Reader, Writer, Events, Content, rules);
    public static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user, string display = "Updated member") where T : IRequestBase => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()),
            new Claim("name", display)], "UnitTest")), new HttpInvocation("PUT", "/risk", "/risk", "risk-test")));
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid tenant, Uuid risk)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), RiskEvaluation.Area, risk.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result;
    }
    public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
    public sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
