using Cntryl.Portia.Testing;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskMethodPublicationTests
{
    [Fact]
    public async Task ShouldReplayOriginalPublicationGivenLaterVersionAndMovedClock()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Request(0, 12);
        var original = await fixture.Handler.HandleAsync(fixture.Context(first), CancellationToken.None);
        Assert.True(original.IsSuccess, original.Error?.Message);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(1);
        var later = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(1, 16)), CancellationToken.None);
        Assert.True(later.IsSuccess, later.Error?.Message);
        fixture.Clock.Now = fixture.Clock.Now.AddYears(1);

        // Act
        var replay = await fixture.Handler.HandleAsync(fixture.Context(first), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(original.Value!.MethodVersionId, replay.Value!.MethodVersionId);
        Assert.Equal(original.Value.Version, replay.Value.Version);
        Assert.Equal(original.Value.PublishedAt, replay.Value.PublishedAt);
        Assert.Equal(original.Value.AppetiteThreshold, replay.Value.AppetiteThreshold);
        var current = await fixture.CurrentAsync();
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(later.Value!.MethodVersionId, current.Value!.MethodVersionId);
    }

    [Fact]
    public async Task ShouldRefuseChangedIntentOrActorGivenAlreadyAcceptedPublicationSlot()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request(0, 12);
        var original = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        Assert.True(original.IsSuccess, original.Error?.Message);

        // Act
        var changed = await fixture.Handler.HandleAsync(fixture.Context(request with { AppetiteThreshold = 13 }), CancellationToken.None);
        var otherActor = await fixture.Handler.HandleAsync(fixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherActor.Error?.Kind);
        var current = await fixture.CurrentAsync();
        Assert.Equal(original.Value!.MethodVersionId, current.Value!.MethodVersionId);
        Assert.Equal(1, current.Value.Version);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public FixedClock Clock { get; } = new();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        public IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        public InMemoryEventStore Events { get; }
        public FitzKvImmutableContentStore Content { get; }
        public PublishRiskMethodVersionHandler Handler => new(new NativeOperationExecutor(Reader, Executor, Content), Reader, Content, Clock);
        public ValueTask<Result<RiskMethodVersionView>> CurrentAsync() => new GetRiskMethodHandler(new RiskMethodReader(Reader, Events, Content))
            .HandleAsync(new RequestContext<GetRiskMethod>(new GetRiskMethod(Tenant, Program), new ClaimsPrincipal()), CancellationToken.None);

        Fixture()
        {
            var store = new InMemoryEventStore();
            Events = store;
            Content = new FitzKvImmutableContentStore(new InMemoryKvClient());
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<IImmutableContentStore>(Content);
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var program = new ComplianceProgram(fixture.Tenant, fixture.Program);
            Assert.Null(program.Create("Risk method program", new ProgramPlan(null, null, null, null, null, null),
                Uuid.CreateVersion4(), "Owner", fixture.Clock.Now));
            await fixture.Writer.SaveAsync(program, new RequestDispatchContext(RequestActor.System));
            return fixture;
        }

        public PublishRiskMethodVersion Request(long expected, int appetite) => new(Tenant, Program, expected,
            RiskMethodTests.Scale(), RiskMethodTests.Scale(), appetite);
        public RequestContext<PublishRiskMethodVersion> Context(PublishRiskMethodVersion request, Uuid? user = null) => new(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", (user ?? User).ToString()),
                new Claim("email", "method@example.test")], "BdgrzSession")));
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    internal sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
