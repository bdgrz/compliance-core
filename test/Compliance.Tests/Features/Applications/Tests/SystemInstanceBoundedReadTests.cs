using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class SystemInstanceBoundedReadTests
{
    const int Backlog = 1_000;

    [Fact]
    public async Task ShouldStopListScanAndReportTransientLagGivenUnrelatedBacklogBeyondScanLimit()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.ProjectToHeadAsync();
        await fixture.AppendUnrelatedAsync(Backlog);
        var handler = new ListSystemInstancesHandler(fixture.Directory, fixture.Consistency(),
            fixture.Visibility());
        var request = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            fixture.TenantId, fixture.ApplicationId), new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        // One record past the limit proves the backlog is larger than the scan.
        Assert.Equal(ApplicationDirectoryBacklog.ScanLimit + 1, fixture.Reader.PatternRecords);
    }

    [Fact]
    public async Task ShouldReadLegacyInstanceWithoutStreamScanGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var legacyId = Uuid.CreateVersion4();
        await fixture.AppendLegacyAsync(legacyId);
        await fixture.ProjectToHeadAsync();
        fixture.Reader.ForbidStreamReads = true;
        var handler = new GetSystemInstanceHandler(fixture.Directory, fixture.Consistency(),
            fixture.Visibility());

        // Act
        var legacy = await handler.HandleAsync(new RequestContext<GetSystemInstance>(
            new GetSystemInstance(fixture.TenantId, fixture.ApplicationId, legacyId,
                MinimumInstanceRevision: 1), new ClaimsPrincipal()), CancellationToken.None);
        var unknown = await handler.HandleAsync(new RequestContext<GetSystemInstance>(
            new GetSystemInstance(fixture.TenantId, fixture.ApplicationId,
                Uuid.CreateVersion4()), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(2, legacy.Value.LegacyApplicationRevision);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(unknown.Error).Kind);
        Assert.Equal(0, fixture.Reader.StreamReads);
    }

    [Fact]
    public async Task ShouldReturnListGivenUnrelatedBacklogOfExactlyScanLimit()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.ProjectToHeadAsync();
        await fixture.AppendUnrelatedAsync(ApplicationDirectoryBacklog.ScanLimit);
        var handler = new ListSystemInstancesHandler(fixture.Directory, fixture.Consistency(),
            fixture.Visibility());
        var request = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            fixture.TenantId, fixture.ApplicationId), new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ApplicationDirectoryBacklog.ScanLimit, fixture.Reader.PatternRecords);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectLosingLegacyParentGivenAnyBacklogSize(bool backlogExceeded)
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var instanceId = Uuid.CreateVersion4();
        var losingParent = Uuid.CreateVersion4();
        await fixture.AppendRegisteredAsync(instanceId);
        await fixture.AppendForeignLegacyAsync(losingParent, instanceId);
        await fixture.ProjectToHeadAsync();
        if (backlogExceeded)
            await fixture.AppendUnrelatedAsync(ApplicationDirectoryBacklog.ScanLimit + 1);
        var legacy = new LegacySystemInstanceSource(fixture.Directory, fixture.Reader);

        // Act
        var exists = await legacy.ExistsAsync(fixture.TenantId, losingParent, instanceId,
            CancellationToken.None);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ShouldReportPendingGivenCommittedButUnprojectedLegacyDeclaration()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.ProjectToHeadAsync();
        var legacyId = Uuid.CreateVersion4();
        await fixture.AppendLegacyAsync(legacyId);
        var activity = new EventSourcedApplicationInventoryActivity(
            new SourceReader(fixture.Source), fixture.Directory,
            new LegacySystemInstanceSource(fixture.Directory, fixture.Reader));

        // Act
        var pending = await activity.GetInstanceStateAsync(fixture.TenantId, legacyId);
        var missing = await activity.GetInstanceStateAsync(fixture.TenantId,
            Uuid.CreateVersion4());

        // Assert
        Assert.Equal(SystemInstanceReferenceState.Pending, pending);
        Assert.Equal(SystemInstanceReferenceState.Missing, missing);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "test"));

    sealed class Fixture
    {
        readonly InMemoryEventStore _events;
        readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
        readonly Uuid _actorId = Uuid.CreateVersion4();
        ulong _applicationVersion;

        Fixture(InMemoryEventStore events, Uuid tenantId, Uuid applicationId,
            DeclaredApplication source)
        {
            _events = events;
            TenantId = tenantId;
            ApplicationId = applicationId;
            Source = source;
            Reader = new GuardedEventReader(events);
            Directory = new FitzApplicationDirectory(new InMemoryKvClient());
        }

        public Uuid TenantId { get; }
        public Uuid ApplicationId { get; }
        public DeclaredApplication Source { get; }
        public GuardedEventReader Reader { get; }
        public FitzApplicationDirectory Directory { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Uuid.CreateVersion4();
            var applicationId = Uuid.CreateVersion4();
            var source = new DeclaredApplication(tenantId, applicationId);
            var fixture = new Fixture(new InMemoryEventStore(), tenantId, applicationId, source);
            Assert.True(source.Declare("Payroll", "Run payroll", null, fixture._actorId,
                "Manager", fixture._now).IsSuccess);
            await fixture.AppendApplicationAsync(new ApplicationDeclared(tenantId,
                applicationId, "Payroll", "Run payroll", null, fixture._actorId, "Manager",
                fixture._now, ApplicationFacts.Application(tenantId, applicationId), null));
            return fixture;
        }

        public SystemInstanceReadConsistency Consistency() =>
            new(Directory, new SourceReader(Source), new LegacySystemInstanceSource(Directory, Reader),
                Reader);

        public RestrictedApplicationVisibility Visibility() =>
            RestrictedApplicationVisibilityFixture.Create(new SourceReader(Source));

        public Task AppendLegacyAsync(Uuid instanceId) =>
            AppendApplicationAsync(new SystemInstanceDeclared(TenantId, ApplicationId,
                instanceId, 2, "Legacy", "production", null, "legacy-source", _actorId,
                "Legacy actor", _now));

        public async Task AppendRegisteredAsync(Uuid instanceId) =>
            await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "system-instances", instanceId.ToString()), 0,
            [
                DomainEventSeed.Attach(new SystemInstanceRegistered(TenantId, ApplicationId,
                    instanceId, 1, "Winner", "production", null, null, _actorId, "Writer",
                    _now, ApplicationFacts.SystemInstance(TenantId, instanceId), null), instanceId, 1),
            ]);

        public async Task AppendForeignLegacyAsync(Uuid applicationId, Uuid instanceId) =>
            await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "applications", applicationId.ToString()), 0,
            [
                DomainEventSeed.Attach(new ApplicationDeclared(TenantId, applicationId,
                    "Other", "Other", null, _actorId, "Manager", _now, ApplicationFacts.Application(TenantId, applicationId), null), applicationId, 1),
                DomainEventSeed.Attach(new SystemInstanceDeclared(TenantId, applicationId,
                    instanceId, 2, "Loser", "production", null, null, _actorId, "Old writer",
                    _now), applicationId, 2),
            ]);

        public async Task AppendBacklogAsync(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var otherId = Uuid.CreateVersion4();
                await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                    "applications", otherId.ToString()), 0,
                [
                    DomainEventSeed.Attach(new ApplicationDeclared(TenantId, otherId,
                        "Other", "Other", null, _actorId, "Manager", _now, ApplicationFacts.Application(TenantId, otherId), null), otherId, 1),
                ]);
            }
        }

        public async Task AppendUnrelatedAsync(int count)
        {
            var stream = new EventStreamAddress(TenantId.ToString(), "unrelated",
                Uuid.CreateVersion4().ToString());
            for (var index = 0; index < count; index++)
                await _events.AppendAsync(stream, (ulong)index,
                [
                    DomainEventSeed.Attach(RbacFacts.TeamDefinedFact(TenantId, Uuid.CreateVersion4(),
                        "Team"), Uuid.CreateVersion4(), (ulong)index + 1),
                ]);
        }

        public async Task ProjectToHeadAsync()
        {
            var identity = new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(TenantId.ToString()));
            var checkpoint = await Directory.LoadCheckpointAsync(TenantId);
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(
                identity, checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(
                               TenantId.ToString()), cursor, CancellationToken.None))
            {
                if (record.Event is ApplicationDeclared or ApplicationRevised or
                    SystemInstanceDeclared or SystemInstanceRegistered)
                    await Directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        async Task AppendApplicationAsync(DomainEvent domainEvent)
        {
            await _events.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "applications", ApplicationId.ToString()), _applicationVersion,
            [
                DomainEventSeed.Attach(domainEvent, ApplicationId,
                    _applicationVersion + 1),
            ]);
            _applicationVersion++;
        }
    }

    sealed class GuardedEventReader(IDomainEventReader inner) : IDomainEventReader
    {
        public int PatternRecords { get; private set; }
        public int StreamReads { get; private set; }
        public bool ForbidStreamReads { get; set; }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, CancellationToken ct = default)
        {
            StreamReads++;
            return ForbidStreamReads
                ? throw new InvalidOperationException("The legacy stream must not be scanned.")
                : inner.ReadAsync(stream, fromVersion, ct);
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, after, ct)
                               .ConfigureAwait(false))
            {
                PatternRecords++;
                yield return record;
            }
        }
    }

    sealed class SourceReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication && aggregate.Id == source.Id
                ? (TAggregate)(Aggregate)source : aggregate);
    }

    sealed class RejectingExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }
    }
}
