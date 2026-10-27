using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
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

sealed class MethodologyTemplateScenario : IAsyncDisposable
{
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    public InMemoryEventStore Events { get; } = new();

    public MethodologyTemplateScenario(DateTimeOffset at)
    {
        Clock = new ScenarioClock(at);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(Events);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
    }

    public ScenarioClock Clock { get; }
    public IAggregateReader Reader { get; }
    public IAggregateWriter Writer { get; }
    public IAggregateExecutor Executor { get; }
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));
    public MethodologyTemplateReader Views => new(Reader, Events, Content);
    public MethodologyTemplateOperations Operations(IImmutableContentStore? store = null) =>
        new(new NativeOperationExecutor(Reader, Executor, store ?? Content),
            new MethodologyTemplateReader(Reader, Events, store ?? Content), store ?? Content, Clock);

    public TemplateApplicationReader ApplicationViews => new(Reader, Events, Content);
    public TemplateApplicationOperations ApplicationOperations(IImmutableContentStore? store = null) =>
        new(Reader, new TemplateApplicationReader(Reader, Events, store ?? Content),
            new NativeOperationExecutor(Reader, Executor, store ?? Content), store ?? Content, Clock);

    public static RequestContext<T> Context<T>(T request, Uuid userId) where T : IRequestBase => new(request,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("name", "Test actor")], "UnitTest")));
    public ValueTask<Result> CreateAsync(CreateMethodologyTemplate request, Uuid userId) =>
        new CreateMethodologyTemplateHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);
    public ValueTask<Result> ReviseAsync(ReviseMethodologyTemplateDraft request, Uuid userId) =>
        new ReviseMethodologyTemplateDraftHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);
    public ValueTask<Result> ReviewAsync(ReviewMethodologyTemplate request, Uuid userId) =>
        new ReviewMethodologyTemplateHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);
    public ValueTask<Result> PublishAsync(ApproveMethodologyTemplateVersion request, Uuid userId) =>
        new ApproveMethodologyTemplateVersionHandler(Operations()).HandleAsync(Context(request, userId), CancellationToken.None);

    public async Task<IReadOnlyList<DomainEvent>> FactsAsync(Uuid templateId)
    {
        var facts = new List<DomainEvent>();
        await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(MethodologyTemplate.Realm,
            MethodologyTemplate.Area, templateId.ToString()), EventCursor.Start, CancellationToken.None))
            facts.Add(record.Event);
        return facts;
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    public sealed class ScenarioClock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = at;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
