using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class ControlSourceFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; } = new();
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public FixtureClock Clock { get; } = new();
    public ControlReader Controls => new(Reader, Events, Content);
    public ControlSourceFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
    }
    public ControlOperations Operations(IImmutableContentStore? content = null) => new(Reader,
        new ControlReader(Reader, Events, content ?? Content), new NativeOperationExecutor(Reader, Executor, content ?? Content),
        content ?? Content, Clock, new SeparationOfDutiesWaiverProofResolver(Reader, new EventStoreSourceFactResolver(Events)));
    public Task SeedAsync(ControlRules rules) => ControlSeeds.AppendAsync(Reader, Writer, Content, rules);
    public async Task SeedProgramAsync(Uuid tenant, Uuid program, Uuid user)
    {
        var owner = new ComplianceProgram(tenant, program);
        Assert.Null(owner.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            RbacIds.Member(tenant, user), "Lead", Clock.Now));
        await Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
    }
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid tenant, Uuid control)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(),
            ControlDraft.Area, control.ToString()), EventCursor.Start, CancellationToken.None))
            facts.Add(record.Event);
        return facts;
    }
    public static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user, bool http = false) where T : IRequestBase => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
            new Claim("sub", user.ToString()), new Claim("name", "Control author")], "UnitTest")),
            http ? new HttpInvocation("PUT", "/synthetic/control", "/synthetic/control", "synthetic") : null));
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
    public sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
