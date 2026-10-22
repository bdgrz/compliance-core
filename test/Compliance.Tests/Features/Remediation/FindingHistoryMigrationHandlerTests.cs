using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingHistoryMigrationHandlerTests
{
    const string MigrationWorker = "migration:FindingHistoryV1";
    static readonly DateTimeOffset Now = new(2026, 10, 21, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldFenceLegacyStreamWithLightweightAggregateGivenFindingHistory()
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var actor = RequestActor.CreateSystem(MigrationWorker, "bdgrz.system");
        var actorReference = ActorReference.ForSystemProcess(MigrationWorker,
            "Finding history migration");
        var dueOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(14);
        var raised = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.",
            "medium", "All", Uuid.CreateVersion4(), dueOn, [], actorReference, Now);
        var marker = new RemediationPartitioningStarted(tenantId, programId, migrationId,
            1, actorReference, Now.AddSeconds(1));
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        await fixture.Events.AppendAsync(sourceStream, 0,
            [DomainEventSeed.Attach(raised, programId, 1, occurredOn: Now)]);
        await fixture.Events.AppendAsync(sourceStream, 1,
            [DomainEventSeed.Attach(marker, programId, 2, occurredOn: Now.AddSeconds(1))]);
        var executor = new RecordingExecutor();
        var handler = new MigrateFindingHistoryHandler(executor, fixture.Reader,
            fixture.Events, TimeProvider.System);
        var request = new MigrateFindingHistory(tenantId, programId, migrationId);

        // Act
        var result = await handler.HandleAsync(new RequestContext<MigrateFindingHistory>(request,
            actor), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("RemediationMigrationFenceAggregate",
            executor.ExecutedAggregates[0].GetType().Name);
        Assert.DoesNotContain(executor.ExecutedAggregates,
            static aggregate => aggregate is RemediationLedger);
    }

    [Fact]
    public async Task ShouldCaptureExactLegacyHeadGivenExistingFindingFacts()
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var dueOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(14);
        var first = new FindingRaised(tenantId, programId, Uuid.CreateVersion4(), 1,
            new FindingSource("manual", null, null, "First gap."), "First gap",
            "First gap found.", "medium", "All", ownerId, dueOn, [], actor, Now);
        var second = new FindingRaised(tenantId, programId, Uuid.CreateVersion4(), 1,
            new FindingSource("manual", null, null, "Second gap."), "Second gap",
            "Second gap found.", "low", "Other scope", ownerId, dueOn, [], actor,
            Now.AddSeconds(1));
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        await fixture.Events.AppendAsync(sourceStream, 0,
        [
            DomainEventSeed.Attach(first, programId, 1, occurredOn: Now),
            DomainEventSeed.Attach(second, programId, 2, occurredOn: Now.AddSeconds(1)),
        ]);
        var request = new MigrateFindingHistory(tenantId, programId, Uuid.CreateVersion4());
        var worker = RequestActor.CreateSystem(MigrationWorker, "bdgrz.system");

        // Act
        var result = await fixture.DispatchAsync(request, worker);
        var records = await fixture.ReadAsync(sourceStream);
        var markerRecord = Assert.Single(records, static record =>
            record.Event is RemediationPartitioningStarted);
        var marker = Assert.IsType<RemediationPartitioningStarted>(markerRecord.Event);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2UL, marker.PreFenceProgramVersion);
        Assert.Equal(2UL, markerRecord.ResourceOffset);
        Assert.Equal(3UL, markerRecord.Event.Metadata.AggregateVersion);
    }

    [Fact]
    public async Task ShouldFenceImportAndRetryFindingHistoryGivenInterleavedProgramEvents()
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var otherFindingId = Uuid.CreateVersion4();
        var migrationId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var dueOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(14);
        var raised = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.",
            "medium", "All", ownerId, dueOn, [], actor, Now);
        var otherRaised = new FindingRaised(tenantId, programId, otherFindingId, 1,
            new FindingSource("manual", null, null, "Other gap."), "Other gap",
            "Other gap found.", "low", "Other scope", ownerId, dueOn, [], actor,
            Now.AddSeconds(1));
        var revised = new FindingRevised(tenantId, programId, findingId, 2, "high",
            ownerId, dueOn.AddDays(10), "All users", "Access was too broad.",
            "Narrowed the affected scope.", actor, Now.AddSeconds(2));
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        await fixture.Events.AppendAsync(sourceStream, 0,
        [
            DomainEventSeed.Attach(raised, programId, 1, occurredOn: Now),
            DomainEventSeed.Attach(otherRaised, programId, 2, occurredOn: Now.AddSeconds(1)),
            DomainEventSeed.Attach(revised, programId, 3, occurredOn: Now.AddSeconds(2)),
        ]);
        var request = new MigrateFindingHistory(tenantId, programId, migrationId);
        var worker = RequestActor.CreateSystem(MigrationWorker, "bdgrz.system");

        // Act
        var start = await fixture.DispatchAsync(request, worker);
        var sourceRecords = await fixture.ReadAsync(sourceStream);
        var fenceRecord = Assert.Single(sourceRecords, static record =>
            record.Event is RemediationPartitioningStarted);
        var fence = Assert.IsType<RemediationPartitioningStarted>(fenceRecord.Event);
        var stepResults = new List<Result>();
        for (ulong offset = 0; offset < fenceRecord.ResourceOffset; offset++)
            stepResults.Add(await fixture.DispatchAsync(request with
            {
                ExpectedSourceOffset = offset,
            }, worker));
        var retryLastStep = await fixture.DispatchAsync(request with
        {
            ExpectedSourceOffset = fenceRecord.ResourceOffset - 1,
        }, worker);
        var complete = await fixture.DispatchAsync(request with
        {
            ExpectedSourceOffset = fenceRecord.ResourceOffset,
        }, worker);
        var retryComplete = await fixture.DispatchAsync(request with
        {
            ExpectedSourceOffset = fenceRecord.ResourceOffset,
        }, worker);
        var targetStream = new FindingAggregate(tenantId, programId, findingId).Stream;
        var targetRecords = await fixture.ReadAsync(targetStream);
        var otherTargetRecords = await fixture.ReadAsync(new FindingAggregate(tenantId,
            programId, otherFindingId).Stream);
        var history = await FindingHistoryFold.ReadAsync(fixture.Events, tenantId, programId,
            findingId, CancellationToken.None);
        var migration = await fixture.Reader.HydrateAsync(new FindingMigrationAggregate(tenantId,
            programId));
        var migrationRecords = await fixture.ReadAsync(
            new FindingMigrationAggregate(tenantId, programId).Stream);

        // Assert
        Assert.True(start.IsSuccess, start.Error?.Message);
        Assert.All(stepResults, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.True(retryLastStep.IsSuccess, retryLastStep.Error?.Message);
        Assert.True(complete.IsSuccess, complete.Error?.Message);
        Assert.True(retryComplete.IsSuccess, retryComplete.Error?.Message);
        Assert.Equal(3UL, fence.PreFenceProgramVersion);
        Assert.Equal(3UL, fenceRecord.ResourceOffset);
        Assert.Equal(4UL, fenceRecord.Event.Metadata.AggregateVersion);
        Assert.Equal(4, targetRecords.Length);
        Assert.Equal(2, otherTargetRecords.Length);
        Assert.Collection(targetRecords,
            record => Assert.IsType<FindingHistoryEventImported>(record.Event),
            record => Assert.IsType<FindingRaised>(record.Event),
            record => Assert.IsType<FindingHistoryEventImported>(record.Event),
            record => Assert.IsType<FindingRevised>(record.Event));
        var markers = targetRecords.Select(static record => record.Event)
            .OfType<FindingHistoryEventImported>().ToArray();
        Assert.Equal([0UL, 2UL], markers.Select(static marker =>
            marker.SourceProgramStreamOffset));
        Assert.Equal([1UL, 3UL], markers.Select(static marker =>
            marker.SourceProgramStreamVersion));
        Assert.Equal(2, history.SourceLineage.Count);
        Assert.Equal(2, history.History.Count);
        Assert.Equal("Gap", history.History[0].Summary);
        Assert.Contains("Narrowed the affected scope.", history.History[1].Summary,
            StringComparison.Ordinal);
        Assert.True(migration.IsComplete);
        Assert.Equal(3UL, migration.NextSourceOffset);
        Assert.Equal(3UL, migration.ImportedFindingEventCount);
        Assert.Equal(5, migrationRecords.Length);
    }

    [Fact]
    public async Task ShouldReadCompletedMigrationFromFindingAggregateGivenLegacyHistory()
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var dueOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(14);
        var raised = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.",
            "medium", "All", ownerId, dueOn, [], actor, Now);
        var revised = new FindingRevised(tenantId, programId, findingId, 2, "high",
            ownerId, dueOn.AddDays(10), "All users", "Access was too broad.",
            "Narrowed the affected scope.", actor, Now.AddSeconds(1));
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        await fixture.Events.AppendAsync(sourceStream, 0,
        [
            DomainEventSeed.Attach(raised, programId, 1, occurredOn: Now),
            DomainEventSeed.Attach(revised, programId, 2, occurredOn: Now.AddSeconds(1)),
        ]);
        var legacy = await fixture.Reader.HydrateAsync(new RemediationLedger(tenantId, programId));
        var expectedHistory = Assert.IsType<FindingView>(legacy.Read(findingId, Now)).History.ToArray();
        var worker = RequestActor.CreateSystem(MigrationWorker, "bdgrz.system");
        var migrationRequest = new MigrateFindingHistory(tenantId, programId,
            Uuid.CreateVersion4());
        Assert.True((await fixture.DispatchAsync(migrationRequest, worker)).IsSuccess);
        var sourceRecords = await fixture.ReadAsync(sourceStream);
        var fenceRecord = Assert.Single(sourceRecords, static record =>
            record.Event is RemediationPartitioningStarted);
        for (ulong offset = 0; offset < fenceRecord.ResourceOffset; offset++)
            Assert.True((await fixture.DispatchAsync(migrationRequest with
            {
                ExpectedSourceOffset = offset,
            }, worker)).IsSuccess);
        Assert.True((await fixture.DispatchAsync(migrationRequest with
        {
            ExpectedSourceOffset = fenceRecord.ResourceOffset,
        }, worker)).IsSuccess);

        using var services = new ServiceCollection()
            .AddSingleton<IDomainEventReader>(fixture.Events)
            .AddSingleton<IAggregateReader>(new LegacySuppressingReader(fixture.Reader))
            .AddSingleton<TimeProvider>(new FixedClock(Now))
            .BuildServiceProvider();
        var handler = ActivatorUtilities.CreateInstance<GetFindingHandler>(services);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetFinding>(new GetFinding(
            tenantId, programId, findingId), worker), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal("high", result.Value.Severity);
        Assert.Equal("Access was too broad.", result.Value.RootCause);
        Assert.Equal(2, result.Value.History.Count);
        Assert.Equal(expectedHistory, result.Value.History);
    }

    [Fact]
    public async Task ShouldReadLegacyFindingWhileMigrationIsIncompleteGivenTargetPrefix()
    {
        // Arrange
        await using var fixture = new Fixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var dueOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(14);
        var raised = new FindingRaised(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Gap."), "Gap", "Gap found.",
            "medium", "All", ownerId, dueOn, [], actor, Now);
        var revised = new FindingRevised(tenantId, programId, findingId, 2, "high",
            ownerId, dueOn.AddDays(10), "All users", "Access was too broad.",
            "Narrowed the affected scope.", actor, Now.AddSeconds(1));
        var sourceStream = new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString());
        await fixture.Events.AppendAsync(sourceStream, 0,
        [
            DomainEventSeed.Attach(raised, programId, 1, occurredOn: Now),
            DomainEventSeed.Attach(revised, programId, 2, occurredOn: Now.AddSeconds(1)),
        ]);
        var worker = RequestActor.CreateSystem(MigrationWorker, "bdgrz.system");
        var migrationRequest = new MigrateFindingHistory(tenantId, programId,
            Uuid.CreateVersion4());
        Assert.True((await fixture.DispatchAsync(migrationRequest, worker)).IsSuccess);
        Assert.True((await fixture.DispatchAsync(migrationRequest with
        {
            ExpectedSourceOffset = 0,
        }, worker)).IsSuccess);
        var handler = new GetFindingHandler(fixture.Reader, fixture.Events,
            new FixedClock(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetFinding>(new GetFinding(
            tenantId, programId, findingId), worker), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal("high", result.Value.Severity);
    }

    [Fact]
    public async Task ShouldRejectUnregisteredMigrationWorkerGivenSystemActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new MigrateFindingHistory(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());

        // Act
        var result = await fixture.DispatchAsync(request, RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(await fixture.ReadAsync(new EventStreamAddress(request.TenantId.ToString(),
            FindingAggregate.LegacySourceArea, request.ProgramId.ToString())));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public InMemoryEventStore Events { get; } = new();
        public IRequestBus Bus { get; }
        public IAggregateReader Reader { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddSingleton<TimeProvider>(new FixedClock(Now));
            services.AddPortia()
                .AddRequestHandler<MigrateFindingHistoryHandler>()
                .AddRequestAuthorizer<MigrateFindingHistoryAuthorizer>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public ValueTask<Result> DispatchAsync(MigrateFindingHistory request,
            System.Security.Claims.ClaimsPrincipal actor) => Bus.DispatchAsync(request,
            new RequestDispatchContext(actor, new DirectInvocation(), new RequestMetadata(
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), null)));

        public async Task<DomainEventRecord[]> ReadAsync(EventStreamAddress stream)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(stream, 0, CancellationToken.None)
                               .ConfigureAwait(false))
                records.Add(record);
            return [.. records];
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class LegacySuppressingReader(IAggregateReader inner) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            aggregate is RemediationLedger
                ? ValueTask.FromResult(aggregate)
                : inner.HydrateAsync(aggregate, ct);
    }

    sealed class RecordingExecutor : IAggregateExecutor
    {
        public List<Aggregate> ExecutedAggregates { get; } = [];

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            ExecutedAggregates.Add(aggregate);
            return ValueTask.FromResult(Result.Success);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new NotSupportedException();
    }
}
