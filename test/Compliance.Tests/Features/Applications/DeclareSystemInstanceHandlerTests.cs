using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclareSystemInstanceHandlerTests
{
    [Fact]
    public async Task ShouldReturnTransientConflictGivenExpectedApplicationRevisionAboveSource()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var handler = scenario.Handler(scenario.Reader);
        var context = scenario.Context(Uuid.CreateVersion4(), expectedApplicationRevision: 2);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Empty(await scenario.InstanceEventsAsync(context.Request.SystemInstanceId));
    }

    [Fact]
    public async Task ShouldWriteOneRegistrationGivenConcurrentDeclarationsWithSameInstanceId()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var gate = new HydrationGate(scenario.Reader, participants: 2);
        var racingHandler = scenario.Handler(gate);
        var context = scenario.Context(Uuid.CreateVersion4(), expectedApplicationRevision: 1);

        // Act
        var racing = await Task.WhenAll(
            racingHandler.HandleAsync(context, CancellationToken.None).AsTask(),
            racingHandler.HandleAsync(context, CancellationToken.None).AsTask());
        var retry = await scenario.Handler(scenario.Reader).HandleAsync(context, CancellationToken.None);
        var events = await scenario.InstanceEventsAsync(context.Request.SystemInstanceId);

        // Assert
        // The losing append rehydrates, resolves the same logical id and replays the winner's registration.
        Assert.All(racing, static outcome => Assert.True(outcome.IsSuccess, outcome.Error?.Message));
        Assert.True(retry.IsSuccess);
        Assert.Equal(context.Request.SystemInstanceId, retry.Value!.SystemInstanceId);
        var registered = Assert.IsType<SystemInstanceRegistered>(Assert.Single(events).Event);
        Assert.Equal(scenario.ApplicationId, registered.ApplicationId);
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenExactRetryAfterApplicationRetired()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var handler = scenario.Handler(scenario.Reader);
        var context = scenario.Context(Uuid.CreateVersion4(), expectedApplicationRevision: 1);
        var first = await handler.HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await scenario.RetireApplicationAsync();

        // Act
        var retry = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value!.SystemInstanceId, retry.Value!.SystemInstanceId);
        Assert.Single(await scenario.InstanceEventsAsync(context.Request.SystemInstanceId));
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameInstanceIdAndChangedDeclaration()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var handler = scenario.Handler(scenario.Reader);
        var context = scenario.Context(Uuid.CreateVersion4(), expectedApplicationRevision: 1);
        var first = await handler.HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = scenario.Context(context.Request.SystemInstanceId, 1, kind: "staging");

        // Act
        var result = await handler.HandleAsync(changed, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(await scenario.InstanceEventsAsync(context.Request.SystemInstanceId));
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly ClaimsPrincipal _actor = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        Scenario(ServiceProvider provider, Uuid tenantId, Uuid applicationId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            TenantId = tenantId;
            ApplicationId = applicationId;
        }

        public Uuid TenantId { get; }
        public Uuid ApplicationId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();

        public static async Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            var scenario = new Scenario(services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true }),
                Uuid.CreateVersion4(), Uuid.CreateVersion4());
            var application = new DeclaredApplication(scenario.TenantId, scenario.ApplicationId);
            Assert.True(application.Declare("Payroll", "Run payroll", null,
                Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
            await scenario.Writer.SaveAsync(application,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            return scenario;
        }

        public async Task RetireApplicationAsync()
        {
            var application = await Reader.HydrateAsync(new DeclaredApplication(TenantId, ApplicationId));
            Assert.Null(application.Retire(application.Revision, DateTimeOffset.UtcNow, "Retired", null,
                Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow));
            await Writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        public DeclareSystemInstanceHandler Handler(IAggregateReader reader) => new(
            new NativeOperationExecutor(reader, _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
                _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>()),
            reader, _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), TimeProvider.System);

        public FixedRequestContext Context(Uuid instanceId, long expectedApplicationRevision, string kind = "production") =>
            new(new DeclareSystemInstance(instanceId, TenantId, ApplicationId, expectedApplicationRevision,
                "Production", kind, null, "payroll-prod"), _actor);

        public async Task<List<DomainEventRecord>> InstanceEventsAsync(Uuid instanceId)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(
                               TenantId.ToString(), "system-instances", instanceId.ToString()),
                               0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    /// <summary>Holds instance hydrations until every racer has loaded the empty stream.</summary>
    sealed class HydrationGate(IAggregateReader inner, int participants) : IAggregateReader
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _arrived;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is not DeclaredSystemInstance)
                return hydrated;
            if (Interlocked.Increment(ref _arrived) >= participants)
                _released.TrySetResult();
            await _released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return hydrated;
        }
    }

    sealed class FixedRequestContext(DeclareSystemInstance request, ClaimsPrincipal actor)
        : IRequestContext<DeclareSystemInstance>
    {
        public DeclareSystemInstance Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => request.SystemInstanceId;
        public Uuid CorrelationId => request.SystemInstanceId;
        public Uuid? CausationId => null;
        public Uuid CauseId => request.SystemInstanceId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}
