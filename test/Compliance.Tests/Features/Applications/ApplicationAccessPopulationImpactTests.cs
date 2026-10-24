using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationAccessPopulationImpactTests
{
    [Fact]
    public async Task ShouldIncludePopulationSnapshotGivenAffectedSystemInstance()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var now = DateTimeOffset.UtcNow;
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var populations = new StubPopulationDirectory(
        [
            new AccessPopulationSummaryView(tenantId, populationId, applicationId,
                systemInstanceId, 1, now.AddDays(-7), 3, AccessPopulation.Accepted, snapshotId,
                new string('a', 64), now.AddDays(-6)),
        ]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, populations, events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var population = Assert.Single(result.Value.AccessPopulationReferences);
        Assert.Equal(populationId, population.PopulationId);
        Assert.Equal(systemInstanceId, population.SystemInstanceId);
        Assert.Equal(snapshotId, population.SnapshotId);
        Assert.DoesNotContain("access_populations", result.Value.PendingContexts);
        Assert.DoesNotContain("access_population_references_over_limit",
            result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepPopulationContextPendingGivenTruncatedInstancePopulationPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var now = DateTimeOffset.UtcNow;
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await ProjectApplicationAsync(applications, tenantId, new SystemInstanceRegistered(
            tenantId, applicationId, systemInstanceId, 1, "Payroll production", "production",
            null, null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.SystemInstance(tenantId, systemInstanceId), null));
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        var populations = new StubPopulationDirectory([], nextCursor: "next");
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, populations, events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("access_populations", result.Value.PendingContexts);
        Assert.Contains("access_population_references_over_limit",
            result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldReportPopulationProjectionLagGivenUnprojectedSourceWrite()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var directory = new StubPopulationDirectory([]);
        var consistency = new ApplicationAccessPopulationReadConsistency(directory, events);
        var opened = new AccessPopulationOpened(tenantId, populationId, Uuid.CreateVersion4(),
            systemInstanceId, 1, DateTimeOffset.UtcNow.AddMinutes(-1), "manual_attestation",
            "Payroll export", ActorReference.ForMember(RbacIds.Member(tenantId,
                Uuid.CreateVersion4()), "Population owner"), DateTimeOffset.UtcNow);
        DomainEvent domainEvent = opened;
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), populationId,
            1, opened.OpenedAt));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            AccessPopulation.Area, populationId.ToString()), 0, [domainEvent]);

        // Act
        var result = await consistency.CaptureAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static PreviewApplicationChangeHandler CreateHandler(Uuid tenantId, Uuid applicationId,
        IAggregateReader sourceReader, FitzApplicationDirectory applications,
        FitzApplicationBoundaryReferenceDirectory boundaries,
        IApplicationControlDraftReferenceDirectory controls,
        IAccessPopulationDirectoryReader populations, InMemoryEventStore events)
    {
        var consistency = new SystemInstanceReadConsistency(applications, sourceReader,
            new LegacySystemInstanceSource(applications, events), events);
        return new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: consistency,
            accessPopulations: populations,
            accessPopulationConsistency: new ApplicationAccessPopulationReadConsistency(
                populations, events));
    }

    static DeclaredApplication CreateApplication(Uuid tenantId, Uuid applicationId)
    {
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StubPopulationDirectory(IReadOnlyList<AccessPopulationSummaryView> items,
        string? nextCursor = null) : IAccessPopulationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessPopulationSummaryView>> ListAsync(Uuid tenantId,
            Uuid systemInstanceId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessPopulationSummaryView>(items.Where(item =>
                item.TenantId == tenantId && item.SystemInstanceId == systemInstanceId)
                .Take(limit).ToArray(), nextCursor));
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
