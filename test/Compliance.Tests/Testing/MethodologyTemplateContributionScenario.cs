using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class MethodologyTemplateContributionScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    readonly InMemoryEventStore _events = new();

    public MethodologyTemplateContributionScenario(DateTimeOffset at)
    {
        Clock = new ScenarioClock(at);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(_events);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
    }

    public ScenarioClock Clock { get; }
    public IAggregateReader Reader { get; }
    public IAggregateExecutor Executor { get; }
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));
    public MethodologyTemplateContributionReader Views => new(Reader, Content);
    public MethodologyTemplateContributionOperations Operations(IImmutableContentStore? store = null) =>
        new(new NativeOperationExecutor(Reader, Executor, store ?? Content), store ?? Content, Clock);

    public static RequestContext<T> Context<T>(T request, Uuid userId) where T : IRequestBase => new(request,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "actor@example.test")], "UnitTest")));

    public ValueTask<Result> ProposeAsync(ProposeDeidentifiedTemplateContribution request, Uuid userId) =>
        new ProposeDeidentifiedTemplateContributionHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);
    public ValueTask<Result> ReviewAsync(ReviewTemplateContribution request, Uuid userId) =>
        new ReviewTemplateContributionHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);
    public ValueTask<Result> AcceptAsync(AcceptTemplateContribution request, Uuid userId) =>
        new AcceptTemplateContributionHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);

    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid tenantId)
    {
        var facts = new List<DomainEvent>();
        await foreach (var item in _events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            "methodology-template-contributions"), EventCursor.Start, CancellationToken.None))
            facts.Add(item.Event);
        return facts;
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    public sealed class ScenarioClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
