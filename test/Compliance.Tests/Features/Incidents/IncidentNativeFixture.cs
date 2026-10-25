using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

sealed class IncidentNativeFixture : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public IncidentNativeFixture()
    {
        var services = new ServiceCollection();
        Receipts = new ReceiptTrackingEventStore(Events, Clock);
        services.AddSingleton<IEventStore>(Receipts);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        Incidents = new ComplianceIncidentReader(Reader, Events, Content, Receipts);
        Operations = new IncidentOperations(new NativeOperationExecutor(Reader, Executor, Content), Incidents, Content, Clock);
    }
    public Uuid Tenant { get; } = Uuid.CreateVersion4();
    public Uuid Program { get; } = Uuid.CreateVersion4();
    public Uuid Incident { get; } = Uuid.CreateVersion4();
    public Uuid User { get; } = Uuid.CreateVersion4();
    public InMemoryEventStore Events { get; } = new();
    public ReceiptTrackingEventStore Receipts { get; }
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public ComplianceIncidentReader Incidents { get; }
    public IncidentOperations Operations { get; }
    public ClockStub Clock { get; } = new();
    public RequestContext<T> Context<T>(T request, Uuid? user = null) where T : IRequestBase => new(request,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", (user ?? User).ToString()), new Claim("name", "Incident member")], "UnitTest")));
    public ComplianceIncidentContent Body(string title = "Production credential incident") => new(null, null, null, null, title, "Exact incident chronology",
        "security", "high", Clock.Now.AddHours(-2), Clock.Now.AddHours(-1), RbacIds.Member(Tenant, User), true, false, [Uuid.CreateVersion4()], "confidential", []);
    public async Task<ComplianceIncidentRules> SeedAsync(ComplianceIncidentContent? content = null, Uuid? id = null)
    {
        var rules = new ComplianceIncidentRules(Tenant, Program, id ?? Incident);
        Assert.Null(rules.Record(content ?? Body(), RbacIds.Member(Tenant, User), "Incident member", Clock.Now));
        await IncidentSeeds.WriteAsync(Writer, Content, rules);
        return rules;
    }
    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid? id = null)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), ComplianceIncident.Area, (id ?? Incident).ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                result.Add(record.Event);
        return result;
    }
    public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
    public sealed class ClockStub : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
