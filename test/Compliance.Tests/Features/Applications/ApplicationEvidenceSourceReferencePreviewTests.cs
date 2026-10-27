using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationEvidenceSourceReferencePreviewTests
{
    [Fact]
    public async Task ShouldIncludeEvidenceSourceGivenAffectedControlAndExactArtifactReadAccess()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory(
            [EvidenceReference(tenantId, programId, controlId, artifactId)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.EvidenceSourceReferences);
        Assert.Equal(artifactId, reference.ArtifactId);
        Assert.Equal(controlId, reference.ControlId);
        Assert.DoesNotContain("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldOmitRestrictedArtifactAndKeepEvidenceContextPendingGivenMissingExactReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory(
            [EvidenceReference(tenantId, programId, controlId, artifactId)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(false), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.EvidenceSourceReferences);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepEvidenceContextPendingGivenUnmappedSupportTypeOrTruncatedPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory([], hasUnresolved: true,
            nextCursor: "next");
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
        Assert.Contains("evidence_source_references_over_limit", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepEvidenceContextPendingGivenImportWorkerWithoutArtifactIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient(), new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(new InMemoryKvClient()));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory(
            [EvidenceReference(tenantId, programId, controlId, artifactId)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.EvidenceSourceReferences);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
    }

    static PreviewApplicationChangeHandler CreateHandler(Uuid tenantId, Uuid applicationId,
        IAggregateReader sourceReader, FitzApplicationDirectory applications,
        FitzApplicationBoundaryReferenceDirectory boundaries,
        IApplicationControlDraftReferenceDirectory controls,
        IApplicationEvidenceSourceReferenceDirectory evidenceSources,
        IApplicationEvidenceSourceAccess artifactReadAccess, InMemoryEventStore events)
    {
        var visibility = RestrictedApplicationVisibilityFixture.Create(sourceReader);
        var controlVersions = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), new ControlProjectionFacts().Content);
        return new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events), visibility,
            instanceConsistency: new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events),
            controlVersions: controlVersions,
            controlVersionConsistency: new ApplicationControlVersionReferenceReadConsistency(
                controlVersions, events),
            evidenceSources: evidenceSources,
            evidenceSourceConsistency: new ApplicationEvidenceSourceReferenceReadConsistency(
                evidenceSources, events),
            evidenceSourceAccess: artifactReadAccess);
    }

    static DeclaredApplication CreateApplication(Uuid tenantId, Uuid applicationId)
    {
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static ApplicationControlDraftReferenceView ControlReference(Uuid tenantId, Uuid programId,
        Uuid applicationId, Uuid controlId) => new(tenantId, "application", applicationId,
        programId, controlId, "AC-01", 1, Uuid.CreateVersion4(), "Payroll",
        "The control applies to payroll.");

    static ApplicationEvidenceSourceReferenceView EvidenceReference(Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid artifactId) => new(tenantId, programId,
        Uuid.CreateVersion4(), controlId, "control_version", 1, artifactId, 1,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Quarterly access evidence");

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        Uuid applicationId)
    {
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId, "Payroll",
            "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StubControlReferenceDirectory(Uuid applicationId,
        ApplicationControlDraftReferenceView reference) : IApplicationControlDraftReferenceDirectory
    {
        public ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<ApplicationControlDraftReferenceView>(subjectType == "application" &&
                recordId == applicationId ? [reference] : [], null));

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubEvidenceSourceReferenceDirectory(
        IReadOnlyList<ApplicationEvidenceSourceReferenceView> items,
        bool hasUnresolved = false, string? nextCursor = null)
        : IApplicationEvidenceSourceReferenceDirectory
    {
        public ValueTask<Page<ApplicationEvidenceSourceReferenceView>> ListByControlAsync(
            Uuid tenantId, Uuid controlId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<ApplicationEvidenceSourceReferenceView>(items.Where(item =>
                item.ControlId == controlId).Take(limit).ToArray(), nextCursor));

        public ValueTask<bool> HasUnresolvedReferencesAsync(Uuid tenantId,
            IReadOnlyCollection<Uuid> programIds, CancellationToken ct = default) =>
            ValueTask.FromResult(hasUnresolved && programIds.Count > 0);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubArtifactReadAccess(bool canRead) : IApplicationEvidenceSourceAccess
    {
        public ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid userId, Uuid artifactId,
            CancellationToken ct = default) => ValueTask.FromResult(canRead && userId != Uuid.Empty);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
