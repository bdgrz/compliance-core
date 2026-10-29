using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramSetupWorkReadConsistencyTests
{
    [Fact]
    public async Task ShouldLoadProgramDirectoryCheckpointGivenBoundTenantPattern()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var directory = new FitzProgramDirectory(new InMemoryKvClient(),
            new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new InMemoryKvClient()));
        var identity = new CheckpointIdentity("ProgramDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var checkpoint = new ProjectionCheckpoint(new EventCursor("1"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
            await batch.CommitAsync(checkpoint);

        // Act
        var actual = await directory.LoadCheckpointAsync(tenantId);
        var wrongArea = await ((IProjectionStore)directory).LoadCheckpointAsync(
            new CheckpointIdentity("ProgramDirectory", EventStreamPattern.ForPattern(
                tenantId.ToString(), "programs")));

        // Assert
        Assert.Equal(checkpoint, actual);
        Assert.Equal(ProjectionCheckpoint.Start, wrongArea);
    }

    [Fact]
    public async Task ShouldLoadBoundaryDirectoryCheckpointGivenBoundTenantPattern()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);
        var identity = new CheckpointIdentity("BoundaryDirectoryV3",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var checkpoint = new ProjectionCheckpoint(new EventCursor("1"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
            await batch.CommitAsync(checkpoint);

        // Act
        var actual = await directory.LoadCheckpointAsync(tenantId);
        var wrongArea = await ((IProjectionStore)directory).LoadCheckpointAsync(
            new CheckpointIdentity("BoundaryDirectoryV3", EventStreamPattern.ForPattern(
                tenantId.ToString(), "boundaries")));

        // Assert
        Assert.Equal(checkpoint, actual);
        Assert.Equal(ProjectionCheckpoint.Start, wrongArea);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenProgramDirectoryCheckpointBehindTenantSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        await AppendProgramCreatedAsync(events, tenantId);
        var consistency = new ProgramSetupWorkReadConsistency(new ProgramDirectory(
            ProjectionCheckpoint.Start), new BoundaryDirectory(ProjectionCheckpoint.Start), events);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenBoundaryDirectoryCheckpointBehindTenantSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        await AppendBoundaryCreatedAsync(events, tenantId);
        var checkpoint = await CheckpointAfterAsync(events, tenantId);
        var consistency = new ProgramSetupWorkReadConsistency(new ProgramDirectory(checkpoint),
            new BoundaryDirectory(ProjectionCheckpoint.Start), events);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldAllowDerivedReadGivenBothDirectoryCheckpointsCaughtUp()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        await AppendProgramCreatedAsync(events, tenantId);
        await AppendBoundaryCreatedAsync(events, tenantId);
        var checkpoint = await CheckpointAfterAsync(events, tenantId);
        var consistency = new ProgramSetupWorkReadConsistency(new ProgramDirectory(checkpoint),
            new BoundaryDirectory(checkpoint), events);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(checkpoint, result.Value.Program);
        Assert.Equal(checkpoint, result.Value.Boundary);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenProjectionCatchesUpAfterDirectoryRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var programs = new ProgramDirectory(ProjectionCheckpoint.Start, new ProgramView(tenantId,
            programId, "SOC 2", "readiness", "type_i", 1,
            new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Lead",
            DateTimeOffset.UtcNow, []));
        var boundaries = new BoundaryDirectory(ProjectionCheckpoint.Start);
        boundaries.OnList = async _ =>
        {
            await AppendProgramCreatedAsync(events, tenantId, programId);
            var checkpoint = await CheckpointAfterAsync(events, tenantId);
            programs.Checkpoint = checkpoint;
            boundaries.Checkpoint = checkpoint;
        };
        var handler = new GetProgramSetupWorkHandler(programs, boundaries,
            new AggregateReader(), new ProgramSetupWorkReadConsistency(programs, boundaries, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgramSetupWork>(
            new GetProgramSetupWork(tenantId, programId), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenSourceChangesAfterProgramDirectoryMiss()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var programs = new ProgramDirectory(ProjectionCheckpoint.Start);
        var boundaries = new BoundaryDirectory(ProjectionCheckpoint.Start);
        programs.OnGet = async _ =>
        {
            await AppendProgramCreatedAsync(events, tenantId, programId);
            var checkpoint = await CheckpointAfterAsync(events, tenantId);
            programs.Checkpoint = checkpoint;
            boundaries.Checkpoint = checkpoint;
        };
        var handler = new GetProgramSetupWorkHandler(programs, boundaries,
            new AggregateReader(), new ProgramSetupWorkReadConsistency(programs, boundaries, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgramSetupWork>(
            new GetProgramSetupWork(tenantId, programId), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenSourceAppendedBetweenFenceCheckpointReads()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var programs = new ProgramDirectory(ProjectionCheckpoint.Start);
        var boundaries = new BoundaryDirectory(ProjectionCheckpoint.Start);
        boundaries.OnLoadCheckpoint = async _ =>
        {
            boundaries.OnLoadCheckpoint = null;
            await AppendBoundaryCreatedAsync(events, tenantId);
            boundaries.Checkpoint = await CheckpointAfterAsync(events, tenantId);
        };
        var consistency = new ProgramSetupWorkReadConsistency(programs, boundaries, events);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldNotReturnSetupWorkGivenSourceEventPendingAfterDirectoryRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var programs = new ProgramDirectory(ProjectionCheckpoint.Start, new ProgramView(tenantId,
            programId, "SOC 2", "readiness", "type_i", 1,
            new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Lead",
            DateTimeOffset.UtcNow, []));
        var boundaries = new BoundaryDirectory(ProjectionCheckpoint.Start)
        {
            OnList = _ => new ValueTask(AppendProgramCreatedAsync(events, tenantId)),
        };
        var handler = new GetProgramSetupWorkHandler(programs, boundaries,
            new AggregateReader(), new ProgramSetupWorkReadConsistency(programs, boundaries, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgramSetupWork>(
            new GetProgramSetupWork(tenantId, programId), new()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    static Task AppendProgramCreatedAsync(InMemoryEventStore events, Uuid tenantId) =>
        AppendProgramCreatedAsync(events, tenantId, Uuid.CreateVersion4());

    static async Task AppendProgramCreatedAsync(InMemoryEventStore events, Uuid tenantId,
        Uuid programId)
    {
        await using var programNative = new ProgramNativeFixture();
        var created = await ProgramSeeds.CreatedAsync(programNative.Content, tenantId, programId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Lead",
            DateTimeOffset.UtcNow);
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), programId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "programs",
            programId.ToString()), 0, [created]);
    }

    static async Task AppendBoundaryCreatedAsync(InMemoryEventStore events, Uuid tenantId)
    {
        var boundaryId = Uuid.CreateVersion4();
        await using var native = new BoundaryProjectionScenario();
        var rules = new BoundaryRules(tenantId, boundaryId);
        Assert.True(rules.Create(Uuid.CreateVersion4(), Uuid.CreateVersion5(boundaryId, "draft-1"),
            new BoundaryContent("Scope", "readiness", ["security"], []), Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var created = Assert.Single(await native.PublishAsync(rules));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "boundaries",
            boundaryId.ToString()), 0, [created]);
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events,
        Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class ProgramDirectory(ProjectionCheckpoint checkpoint, ProgramView? program = null)
        : IProgramDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;
        public Func<CancellationToken, ValueTask>? OnGet { get; set; }

        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => GetAsync(ct);

        async ValueTask<ProgramView?> GetAsync(CancellationToken ct)
        {
            if (OnGet is not null)
                await OnGet(ct);
            return program;
        }

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);
    }

    sealed class BoundaryDirectory(ProjectionCheckpoint checkpoint) : IBoundaryDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;
        public Func<CancellationToken, ValueTask>? OnList { get; set; }
        public Func<CancellationToken, ValueTask>? OnLoadCheckpoint { get; set; }

        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(null);

        public async ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            if (OnList is not null)
                await OnList(ct);
            return new Page<BoundaryView>([], null);
        }

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(null);

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => LoadCheckpointAsync(ct);

        async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct)
        {
            if (OnLoadCheckpoint is not null)
                await OnLoadCheckpoint(ct);
            return Checkpoint;
        }
    }

    sealed class AggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate => ValueTask.FromResult(aggregate);
    }
}
