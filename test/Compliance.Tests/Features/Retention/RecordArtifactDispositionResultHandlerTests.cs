using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class RecordArtifactDispositionResultHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 15, 0, 0, TimeSpan.Zero);
    const string Worker = "reactor:ArtifactDispositionExecutionV1";

    [Fact]
    public async Task ShouldRecordSuccessfulResultGivenExactPendingAttemptAndNamedWorker()
    {
        // Arrange
        await using var fixture = new Fixture();
        var attempt = await fixture.StartExecutionAsync();
        var request = new RecordArtifactDispositionResult(fixture.TenantId,
            fixture.DispositionId, fixture.ArtifactId, attempt.Revision, attempt.Number,
            attempt.EventId, "disposed", null, Now.AddHours(2));
        // Act
        var result = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), attempt.EventId);
        var retry = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), attempt.EventId);
        var saved = await fixture.Reader.HydrateAsync(new ArtifactDisposition(fixture.TenantId,
            fixture.DispositionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("disposed", saved.Current!.Status);
        Assert.Equal(3, saved.Revision);
        Assert.Equal("disposed", Assert.Single(saved.Current.Attempts).Outcome);
        Assert.Equal(fixture.ArtifactId, saved.Current.Tombstone!.ArtifactId);
    }

    [Fact]
    public async Task ShouldRejectLateResultGivenANewerExecutionAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.StartExecutionAsync();
        var failed = await fixture.Executor.ExecuteAsync(new ArtifactDisposition(fixture.TenantId,
                fixture.DispositionId), disposition => AggregateOutcome.CommitOnSuccess(
                disposition.RecordExecutionResult(first.Revision, "failed", "storage_timeout",
                    fixture.WorkerActor, Now.AddHours(2))),
            new RequestDispatchContext(RequestActor.System));
        Assert.True(failed.IsSuccess, failed.Error?.Message);
        var second = await fixture.StartExecutionAsync();
        var late = new RecordArtifactDispositionResult(fixture.TenantId,
            fixture.DispositionId, fixture.ArtifactId, first.Revision, first.Number,
            first.EventId, "disposed", null, Now.AddHours(3));
        // Act
        var result = await fixture.DispatchAsync(late,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), first.EventId);
        var saved = await fixture.Reader.HydrateAsync(new ArtifactDisposition(fixture.TenantId,
            fixture.DispositionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(second.Revision, saved.Revision);
        Assert.Equal("executing", saved.Current!.Status);
        Assert.Equal(2, saved.Current.Attempts.Count);
        Assert.Equal("requested", saved.Current.Attempts[^1].Status);
    }

    [Fact]
    public async Task ShouldRejectResultGivenNonWorkerActorOrMismatchedCausation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var attempt = await fixture.StartExecutionAsync();
        var request = new RecordArtifactDispositionResult(fixture.TenantId,
            fixture.DispositionId, fixture.ArtifactId, attempt.Revision, attempt.Number,
            attempt.EventId, "disposed", null, Now.AddHours(2));
        // Act
        var nonWorker = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem("reactor:OtherWorker", "bdgrz.system"), attempt.EventId);
        var wrongCause = await fixture.DispatchAsync(request,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), Uuid.CreateVersion4());
        var fabricatedEventId = Uuid.CreateVersion4();
        var fabricatedSource = request with { ExecutionRequestEventId = fabricatedEventId };
        var missingEvent = await fixture.DispatchAsync(fabricatedSource,
            RequestActor.CreateSystem(Worker, "bdgrz.system"), fabricatedEventId);
        var saved = await fixture.Reader.HydrateAsync(new ArtifactDisposition(fixture.TenantId,
            fixture.DispositionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, nonWorker.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongCause.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, missingEvent.Error?.Kind);
        Assert.Equal("executing", saved.Current!.Status);
        Assert.Equal("requested", Assert.Single(saved.Current.Attempts).Status);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IDomainEventReader _events;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ArtifactId { get; } = Uuid.CreateVersion4();
        public Uuid DispositionId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        public IRequestBus Bus { get; }
        public IDomainEventReader Events => _events;
        public ActorReference AdminActor { get; } = ActorReference.ForMember(
            Uuid.CreateVersion4(), "Org Admin");
        public ActorReference WorkerActor { get; } = ActorReference.ForSystemProcess(Worker,
            "Artifact disposition worker");

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddPortia()
                .AddRequestHandler<RecordArtifactDispositionResultHandler>()
                .AddRequestAuthorizer<RecordArtifactDispositionResultAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            _events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        }

        public ValueTask<Result> DispatchAsync(RecordArtifactDispositionResult request,
            System.Security.Claims.ClaimsPrincipal actor, Uuid? causationId) =>
            Bus.DispatchAsync(request, new RequestDispatchContext(actor, new DirectInvocation(),
                new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), causationId)));

        public async Task<ExecutionAttempt> StartExecutionAsync()
        {
            var approved = await Executor.ExecuteAsync(new ArtifactDisposition(TenantId, DispositionId),
                disposition => AggregateOutcome.CommitOnSuccess(disposition.Approve(Eligible(), 0,
                    "Retention elapsed with no holds or reliance", AdminActor, Now)),
                new RequestDispatchContext(RequestActor.System));
            if (!approved.IsSuccess)
                throw new InvalidOperationException(approved.Error?.Message);

            var current = await Reader.HydrateAsync(new ArtifactDisposition(TenantId, DispositionId));
            var requested = await Executor.ExecuteAsync(new ArtifactDisposition(TenantId, DispositionId),
                disposition => AggregateOutcome.CommitOnSuccess(disposition.RequestExecution(
                    current.Revision, Eligible(), WorkerActor, Now.AddHours(current.Revision))),
                new RequestDispatchContext(RequestActor.System));
            if (!requested.IsSuccess)
                throw new InvalidOperationException(requested.Error?.Message);

            var saved = await Reader.HydrateAsync(new ArtifactDisposition(TenantId, DispositionId));
            var attemptNumber = saved.Current!.Attempts[^1].Number;
            var requestEvent = await ReadExecutionEventAsync(attemptNumber);
            return new ExecutionAttempt(saved.Revision, saved.Current!.Attempts[^1].Number,
                requestEvent.Event.Metadata.EventId);
        }

        async Task<DomainEventRecord> ReadExecutionEventAsync(int attemptNumber)
        {
            await foreach (var record in _events.ReadAsync(new ArtifactDisposition(TenantId,
                               DispositionId).Stream, 0))
                if (record.Event is ArtifactDispositionExecutionRequested requested &&
                    requested.State.Attempts[^1].Number == attemptNumber)
                    return record;
            throw new InvalidOperationException("The execution request event was not persisted.");
        }

        ArtifactDispositionEligibilitySnapshot Eligible() => new(
            new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId,
                new string('a', 64)), 12, 4096, "available", null, 1, "restricted", true,
            "m0_d16_v1", new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31),
            new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1), [], [], true);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed record ExecutionAttempt(long Revision, int Number, Uuid EventId);

}
