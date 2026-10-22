using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FindingWorkProjectionCutoverTests
{
    [Fact]
    public async Task ShouldIsolateWorkGivenSameFindingAndActionIdentitiesInDifferentPrograms()
    {
        // Arrange
        using var fixture = new Fixture();
        var otherProgram = Uuid.CreateVersion4();
        var actions = fixture.Actions();
        var closures = fixture.Closures();
        await using (var actionBatch = await actions.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzCorrectiveActionWorkItemDirectory.ProjectorName, actions.SourcePattern(fixture.TenantId)), ProjectionCheckpoint.Start)))
        await using (var closureBatch = await closures.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzFindingClosureWorkItemDirectory.ProjectorName, closures.SourcePattern(fixture.TenantId)), ProjectionCheckpoint.Start)))
        {
            foreach (var program in new[] { fixture.ProgramId, otherProgram })
            {
                DomainEvent[] facts =
                [
                    new FindingRaised(fixture.TenantId, program, fixture.FindingId, 1,
                        new FindingSource("manual", null, null, "Gap"), "Gap", "Gap", "high", "All",
                        fixture.OwnerId, fixture.DueOn, [], fixture.Actor, fixture.Now),
                    new CorrectiveActionAdded(fixture.TenantId, program, fixture.FindingId, 2,
                        new CorrectiveActionView(fixture.ActionId, "Repair", fixture.OwnerId, fixture.DueOn,
                            "open", fixture.Actor, fixture.Now)),
                ];
                foreach (var fact in facts)
                {
                    await FindingWorkProjectionTestSource.ApplyAsync(actions, fact);
                    await FindingWorkProjectionTestSource.ApplyAsync(closures, FindingEventFacts.Freeze(fact));
                }
            }
            var revised = new FindingRevised(fixture.TenantId, fixture.ProgramId, fixture.FindingId, 3,
                "low", fixture.OwnerId, fixture.DueOn, "All", null, "Reassessed", fixture.Actor, fixture.Now);
            var completed = new CorrectiveActionCompleted(fixture.TenantId, otherProgram, fixture.FindingId, 3,
                fixture.ActionId, "Done", [], fixture.OwnerId, fixture.Actor, fixture.Now);
            foreach (var fact in new DomainEvent[] { revised, completed })
            {
                await FindingWorkProjectionTestSource.ApplyAsync(actions, fact);
                await FindingWorkProjectionTestSource.ApplyAsync(closures, FindingEventFacts.Freeze(fact));
            }
            await actionBatch.CommitAsync(ProjectionCheckpoint.Start);
            await closureBatch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var firstAction = await actions.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId);
        var secondAction = await actions.LoadProjectedProgramAsync(fixture.TenantId, otherProgram);
        var firstClosure = await closures.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId);
        var secondClosure = await closures.LoadProjectedProgramAsync(fixture.TenantId, otherProgram);

        // Assert
        Assert.Equal("low", Assert.Single(firstAction.Value).Materiality);
        Assert.Equal(WorkCandidate.IdFor(fixture.ActionId, WorkSource.CorrectiveAction), firstAction.Value[0].WorkItemId);
        Assert.Empty(secondAction.Value);
        Assert.Empty(firstClosure.Value);
        Assert.Single(secondClosure.Value);
    }

    [Fact]
    public void ShouldUseFreshCoherentProjectionGenerationGivenFindingStreamCutover()
    {
        // Arrange
        using var fixture = new Fixture();
        var actions = fixture.Actions();
        var closures = fixture.Closures();

        // Act
        var actionProjector = new CorrectiveActionWorkItemProjector(actions);
        var closureProjector = new FindingClosureWorkItemProjector(closures);

        // Assert
        Assert.Equal("AccountableWorkItemCorrectiveActionV2", actionProjector.Name);
        Assert.Equal("AccountableWorkItemFindingClosureV2", closureProjector.Name);
        Assert.Equal(EventStreamPattern.ForTenant(), actionProjector.Pattern);
        Assert.Equal(EventStreamPattern.ForTenant(), closureProjector.Pattern);
        Assert.Equal(EventStreamPattern.ForPattern(fixture.TenantId.ToString()), actions.SourcePattern(fixture.TenantId));
        Assert.Equal(actions.SourcePattern(fixture.TenantId), closures.SourcePattern(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldPreserveLegacyWorkGivenUnmigratedFindingAndEmptyProjection()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: false);

        // Act
        var actions = await fixture.Actions().LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(actions.IsSuccess, actions.Error?.Message);
        var action = Assert.Single(actions.Value);
        Assert.Equal(WorkCandidate.IdFor(fixture.ActionId, WorkSource.CorrectiveAction), action.WorkItemId);
        Assert.Equal(fixture.OwnerId, action.Responsible!.Id);
    }

    [Fact]
    public async Task ShouldKeepPartialMigrationInvisibleGivenLegacyClosureWork()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: true);
        await fixture.MigrateAsync(null);
        await fixture.MigrateAsync(0);
        var closures = fixture.Closures();
        await fixture.CatchUpAsync(closures, closures, closures.ApplyAsync);

        // Act
        var result = await closures.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var closure = Assert.Single(result.Value);
        Assert.Contains("revision 3", closure.Reason, StringComparison.Ordinal);
        Assert.True(closure.Excluded.Contains(fixture.OwnerId));
    }

    [Fact]
    public async Task ShouldProjectImportedBusinessFactsOnceGivenCompletedMigrationAndReplay()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: true);
        await fixture.MigrateAsync(null);
        for (ulong offset = 0; offset <= 3; offset++)
            await fixture.MigrateAsync(offset);
        var closures = fixture.Closures();
        var actions = fixture.Actions();

        // Act
        await fixture.CatchUpAsync(closures, closures, closures.ApplyAsync);
        await fixture.CatchUpAsync(actions, actions, actions.ApplyAsync);
        await fixture.CatchUpAsync(closures, closures, closures.ApplyAsync);
        var closureResult = await closures.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);
        var actionResult = await actions.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(closureResult.IsSuccess, closureResult.Error?.Message);
        Assert.True(actionResult.IsSuccess, actionResult.Error?.Message);
        var closure = Assert.Single(closureResult.Value);
        Assert.Equal(WorkCandidate.IdFor(Uuid.CreateVersion5(fixture.FindingId, "finding-closure\n3"), FindingClosureWork.Kind), closure.WorkItemId);
        Assert.Empty(actionResult.Value);
        Assert.Equal(3, await actions.LoadRevisionAsync(fixture.TenantId));
        Assert.True((await new WorkQueueReadConsistency(fixture.Events, [closures, actions])
            .CaptureAsync(fixture.TenantId, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task ShouldFenceLegacyFallbackGivenSourceChangesAfterQueueCapture()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: false);
        var actions = fixture.Actions();
        await fixture.CatchUpAsync(actions, actions, actions.ApplyAsync);
        var consistency = new WorkQueueReadConsistency(fixture.Events, [actions]);
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        Assert.True(captured.IsSuccess);

        // Act
        await fixture.Events.AppendAsync(fixture.LegacyStream, 2,
            [DomainEventSeed.Attach(new FindingRevised(fixture.TenantId, fixture.ProgramId,
                fixture.FindingId, 3, "critical", fixture.OwnerId, fixture.DueOn, "All", null,
                "Scope reviewed.", fixture.Actor, fixture.Now.AddMinutes(2)), fixture.ProgramId,
                3, occurredOn: fixture.Now.AddMinutes(2))]);
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.False(confirmed.IsSuccess);
        Assert.True(confirmed.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectEmptyProjectionGivenCompletedMigrationWithOutstandingAction()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: false);
        await fixture.MigrateAsync(null);
        for (ulong offset = 0; offset <= 2; offset++)
            await fixture.MigrateAsync(offset);

        // Act
        var result = await fixture.Actions().LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRunCanonicalProjectorsGivenLegacyFactsAndPairedImportedFacts()
    {
        // Arrange
        using var fixture = new Fixture();
        await fixture.SeedLegacyAsync(completed: true);
        await fixture.MigrateAsync(null);
        for (ulong offset = 0; offset <= 3; offset++)
            await fixture.MigrateAsync(offset);
        var closures = fixture.Closures();
        var actions = fixture.Actions();

        // Act
        await fixture.RunAsync(new FindingClosureWorkItemProjector(closures), closures);
        await fixture.RunAsync(new CorrectiveActionWorkItemProjector(actions), actions);
        var result = await closures.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value);
        Assert.Equal(3, await actions.LoadRevisionAsync(fixture.TenantId));
        Assert.True((await new WorkQueueReadConsistency(fixture.Events, [closures, actions])
            .CaptureAsync(fixture.TenantId, CancellationToken.None)).IsSuccess);
    }

    sealed class Fixture : IDisposable
    {
        readonly ServiceProvider _provider;
        readonly IServiceScope _scope;
        readonly Uuid _migrationId = Uuid.CreateVersion4();
        readonly InMemoryKvClient _client = new();
        public InMemoryEventStore Events { get; } = new();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid FindingId { get; } = Uuid.CreateVersion4();
        public Uuid ActionId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset Now { get; } = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        public DateOnly DueOn => DateOnly.FromDateTime(Now.UtcDateTime).AddDays(7);
        public ActorReference Actor => ActorReference.ForMember(OwnerId, "Owner");
        public EventStreamAddress LegacyStream => new(TenantId.ToString(), "remediation", ProgramId.ToString());

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddScoped<FindingReadSource>();
            services.AddPortia().AddRequestHandler<MigrateFindingHistoryHandler>()
                .AddRequestAuthorizer<MigrateFindingHistoryAuthorizer>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
        }

        public FitzCorrectiveActionWorkItemDirectory Actions() => new(_client,
            _scope.ServiceProvider.GetRequiredService<FindingReadSource>(), TimeProvider.System);
        public FitzFindingClosureWorkItemDirectory Closures() => new(_client,
            _scope.ServiceProvider.GetRequiredService<FindingReadSource>(), TimeProvider.System);

        public async Task SeedLegacyAsync(bool completed)
        {
            var facts = new List<DomainEvent>
            {
                new FindingRaised(TenantId, ProgramId, FindingId, 1,
                    new FindingSource("manual", null, null, "Gap"), "Gap", "Gap", "high",
                    "All", OwnerId, DueOn, [], Actor, Now),
                new CorrectiveActionAdded(TenantId, ProgramId, FindingId, 2,
                    new CorrectiveActionView(ActionId, "Correct it", OwnerId, DueOn,
                        FindingStates.Open, Actor, Now.AddMinutes(1))),
            };
            if (completed)
                facts.Add(new CorrectiveActionCompleted(TenantId, ProgramId, FindingId, 3,
                    ActionId, "Corrected", [], OwnerId, Actor, Now.AddMinutes(2)));
            await Events.AppendAsync(LegacyStream, 0, facts.Select((fact, index) =>
                DomainEventSeed.Attach(fact, ProgramId, (ulong)index + 1,
                    occurredOn: Now.AddMinutes(index))).ToArray());
        }

        public async Task MigrateAsync(ulong? offset)
        {
            var result = await _scope.ServiceProvider.GetRequiredService<IRequestBus>()
                .DispatchAsync(new MigrateFindingHistory(TenantId, ProgramId, _migrationId, offset),
                    new RequestDispatchContext(RequestActor.CreateSystem("migration:FindingHistoryV1", "bdgrz.system"),
                        new DirectInvocation(), new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null)));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        public async Task CatchUpAsync(IAccountableWorkItemDirectoryReader directory,
            IProjectionStore store, Func<DomainEvent, CancellationToken, ValueTask> apply)
        {
            var checkpoint = await directory.LoadCheckpointAsync(TenantId);
            var cursor = checkpoint.Cursor;
            await using var batch = await store.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(directory.ProjectorName, directory.SourcePattern(TenantId)), checkpoint));
            await foreach (var record in Events.ReadAsync(directory.SourcePattern(TenantId), cursor, CancellationToken.None))
            {
                await apply(record.Event, CancellationToken.None);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public async Task RunAsync(Projector projector, IAccountableWorkItemDirectoryReader directory)
        {
            await new ProjectorScenario(new TenantId(TenantId.ToString())).RunAsync(projector);
            var checkpoint = await directory.LoadCheckpointAsync(TenantId);
            var runner = new ProjectorRunner(Events);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    return;
                checkpoint = next;
            }
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
