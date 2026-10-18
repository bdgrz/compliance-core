using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationReadinessImpactTests
{
    [Fact]
    public async Task ShouldReturnExactApplicationAndSystemInstanceSourcesGivenLatestReadinessAssessment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var otherApplicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(actorId, "Compliance Lead");
        var program = new ProgramView(tenantId, programId, "SOC 2", "readiness", "type_i", 1,
            new ProgramPlan(null, null, null, null, null, null), actorId, actor.Display,
            now, []);
        var assessment = new ReadinessAssessmentView(tenantId, programId, assessmentId, 1,
            "readiness-rules/12", now, null, "fingerprint",
            [new ReadinessInputView("applications_access_review_scope", "assessed", 1,
                "Reviewed system instances."),
             new ReadinessInputView("evidence", "not_assessed", 0,
                "Evidence readiness is not assessed.")],
            [new ReadinessFindingView("CC1.1", "security", "Description", "mapping",
                "rule_met", "Mapping was in force.",
                [new ReadinessSourceReference("application", applicationId, "4"),
                 new ReadinessSourceReference("system_instance", systemInstanceId, null),
                 new ReadinessSourceReference("application", otherApplicationId, "1")],
                null)], [], 1, 0, actor, now, null)
        {
            AccessReviewScopes = [new ReadinessAccessReviewScopeView(systemInstanceId,
                "unresolved", false, null)],
        };
        var programs = new StubProgramDirectory([program]);
        var readiness = new StubReadinessDirectory(programId, assessment);
        var events = new InMemoryEventStore();
        var reader = Reader(programs, readiness, events);

        // Act
        var result = await reader.ReadAsync(tenantId, applicationId, [systemInstanceId],
            systemInstanceListComplete: true, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value.References.Count);
        Assert.Contains(result.Value.References, reference =>
            reference.Source.Kind == "application" && reference.Source.Id == applicationId &&
            reference.Source.Version == "4");
        Assert.Contains(result.Value.References, reference =>
            reference.Source.Kind == "system_instance" && reference.Source.Id == systemInstanceId &&
            reference.Source.Version is null);
        Assert.DoesNotContain(result.Value.References, reference =>
            reference.Source.Id == otherApplicationId);
        Assert.False(result.Value.Complete);
        Assert.False(result.Value.ReferencesOverLimit);
    }

    [Fact]
    public async Task ShouldKeepReadinessIncompleteGivenMoreThanTwoHundredMatchingSources()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(actorId, "Compliance Lead");
        var program = new ProgramView(tenantId, programId, "SOC 2", "readiness", "type_i", 1,
            new ProgramPlan(null, null, null, null, null, null), actorId, actor.Display,
            now, []);
        var sources = Enumerable.Range(1, 201)
            .Select(index => new ReadinessSourceReference("application", applicationId,
                index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        var assessment = new ReadinessAssessmentView(tenantId, programId, assessmentId, 1,
            "readiness-rules/12", now, null, "fingerprint", [],
            [new ReadinessFindingView("CC1.1", "security", "Description", "mapping",
                "rule_met", "Mapping was in force.", sources, null)], [], 1, 0, actor, now,
            null);
        var reader = Reader(new StubProgramDirectory([program]),
            new StubReadinessDirectory(programId, assessment), new InMemoryEventStore());

        // Act
        var result = await reader.ReadAsync(tenantId, applicationId, [],
            systemInstanceListComplete: true, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(200, result.Value.References.Count);
        Assert.True(result.Value.ReferencesOverLimit);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldKeepReadinessPendingGivenProgramDirectoryOverflow()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessment = Assessment(tenantId, programId);
        var program = Program(tenantId, programId);
        var reader = Reader(new StubProgramDirectory([program], "next"),
            new StubReadinessDirectory(programId, assessment), new InMemoryEventStore());

        // Act
        var result = await reader.ReadAsync(tenantId, Uuid.CreateVersion4(), [], true,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value.Complete);
        Assert.True(result.Value.ProgramsOverLimit);
        Assert.False(result.Value.ReferencesOverLimit);
    }

    [Fact]
    public async Task ShouldKeepReadinessPendingGivenLatestAssessmentWithoutDirectMatches()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var reader = Reader(new StubProgramDirectory([Program(tenantId, programId)]),
            new StubReadinessDirectory(programId, Assessment(tenantId, programId)),
            new InMemoryEventStore());

        // Act
        var result = await reader.ReadAsync(tenantId, Uuid.CreateVersion4(), [], true,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.References);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenReadinessProjectionBehindSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var now = DateTimeOffset.UtcNow;
        var actorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(actorId, "Compliance Lead");
        var sourceEvent = new ReadinessAssessmentRecorded(tenantId, programId, 1,
            Uuid.CreateVersion4(), "readiness-rules/12", now, null, "fingerprint", [], [], [],
            actorId, actor, now);
        sourceEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), tenantId, 1,
            now));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            ReadinessLedger.Area, programId.ToString()), 0, [sourceEvent]);
        var reader = Reader(new StubProgramDirectory([Program(tenantId, programId)]),
            new StubReadinessDirectory(programId, Assessment(tenantId, programId)), events);

        // Act
        var result = await reader.ReadAsync(tenantId, Uuid.CreateVersion4(), [], true,
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenProgramDirectoryBehindSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var now = DateTimeOffset.UtcNow;
        var actorId = Uuid.CreateVersion4();
        var sourceEvent = new ProgramCreated(tenantId, programId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null), actorId, "Compliance Lead",
            now);
        sourceEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), tenantId, 1,
            now));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "programs",
            programId.ToString()), 0, [sourceEvent]);
        var reader = Reader(new StubProgramDirectory([Program(tenantId, programId)]),
            new StubReadinessDirectory(programId, Assessment(tenantId, programId)), events);

        // Act
        var result = await reader.ReadAsync(tenantId, Uuid.CreateVersion4(), [], true,
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public void ShouldCanonicalizeReadinessReferencesGivenImpactDigest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var first = ReadinessReference(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "system_instance");
        var second = ReadinessReference(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "application");
        var preview = new ApplicationChangePreview(tenantId, applicationId, 1, "retire",
            [], [], [], false)
        {
            ReadinessReferences = [first, second],
        };

        // Act
        var digest = ApplicationChangeImpactDigest.Compute(preview);
        var reorderedDigest = ApplicationChangeImpactDigest.Compute(preview with
        {
            ReadinessReferences = [second, first],
        });
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(preview,
            ComplianceCoreJsonContext.Default.ApplicationChangePreview));

        // Assert
        Assert.Equal(digest, reorderedDigest);
        Assert.Equal(2, json.RootElement.GetProperty("readiness_references").GetArrayLength());
    }

    [Fact]
    public async Task ShouldIncludeReadinessSourcesGivenApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var sourceReader = new SourceReader(source);
        var events = new InMemoryEventStore();
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await using (var batch = await applications.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("ApplicationDirectoryV2",
                             EventStreamPattern.ForPattern(tenantId.ToString())),
                         ProjectionCheckpoint.Start)))
        {
            await applications.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await applications.ApplyAsync(new SystemInstanceRegistered(tenantId, applicationId,
                systemInstanceId, 1, "Payroll production", "production", null, null,
                actorId, "Manager", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var program = Program(tenantId, programId);
        var actor = ActorReference.ForMember(actorId, "Manager");
        var assessment = new ReadinessAssessmentView(tenantId, programId, assessmentId, 1,
            "readiness-rules/12", now, null, "fingerprint",
            [new ReadinessInputView("evidence", "not_assessed", 0,
                "Evidence is not assessed.")],
            [new ReadinessFindingView("CC1.1", "security", "Description", "technology",
                "gap", "A referenced application source.",
                [new ReadinessSourceReference("application", applicationId, "1"),
                 new ReadinessSourceReference("system_instance", systemInstanceId, null)],
                Uuid.CreateVersion4())], [], 0, 1, actor, now, null);
        var readinessDirectory = new StubReadinessDirectory(programId, assessment);
        var readinessImpactReader = Reader(new StubProgramDirectory([program]),
            readinessDirectory, events);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var handler = new PreviewApplicationChangeHandler(sourceReader, applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events),
            RestrictedApplicationVisibilityFixture.Create(sourceReader),
            instanceConsistency: new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events),
            readinessImpactReader: readinessImpactReader);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(tenantId, applicationId,
            1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value.ReadinessReferences.Count);
        Assert.Contains("readiness", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    static ApplicationReadinessImpactReader Reader(StubProgramDirectory programs,
        StubReadinessDirectory readiness, InMemoryEventStore events)
    {
        var readinessConsistency = new ReadinessProjectionReadConsistency(null!, null!, null!,
            null!, null!, readiness, events);
        return new ApplicationReadinessImpactReader(programs, readiness,
            new ProgramDirectoryReadConsistency(programs, events), readinessConsistency);
    }

    static ProgramView Program(Uuid tenantId, Uuid programId)
    {
        var actorId = Uuid.CreateVersion4();
        return new ProgramView(tenantId, programId, "SOC 2", "readiness", "type_i", 1,
            new ProgramPlan(null, null, null, null, null, null), actorId, "Compliance Lead",
            DateTimeOffset.UtcNow, []);
    }

    static ReadinessAssessmentView Assessment(Uuid tenantId, Uuid programId)
    {
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
        return new ReadinessAssessmentView(tenantId, programId, Uuid.CreateVersion4(), 1,
            "readiness-rules/12", DateTimeOffset.UtcNow, null, "fingerprint", [], [], [], 0, 0,
            actor, DateTimeOffset.UtcNow, null);
    }

    static ApplicationReadinessReferenceView ReadinessReference(Uuid programId,
        Uuid assessmentId, string sourceKind) => new(programId, "SOC 2", assessmentId, 1,
        "readiness-rules/12", DateTimeOffset.UnixEpoch,
        new ReadinessSourceReference(sourceKind, Uuid.CreateVersion4(), null));

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    sealed class StubProgramDirectory(IReadOnlyList<ProgramView> items,
        string? nextCursor = null) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(items
            .SingleOrDefault(program => program.TenantId == tenantId &&
                                        program.ProgramId == programId));

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>(
            items.Where(program => program.TenantId == tenantId).Take(limit).ToArray(), nextCursor));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubReadinessDirectory(Uuid programId, ReadinessAssessmentView assessment)
        : IReadinessDirectoryReader
    {
        public ValueTask<Result<Uuid?>> GetLatestAssessmentIdAsync(Uuid tenantId,
            Uuid requestedProgramId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<Uuid?>.Success(requestedProgramId == programId &&
                assessment.TenantId == tenantId ? assessment.AssessmentId : null));

        public ValueTask<ReadinessAssessmentView?> GetAssessmentAsync(Uuid tenantId,
            Uuid requestedProgramId, Uuid assessmentId, CancellationToken ct = default) =>
            ValueTask.FromResult<ReadinessAssessmentView?>(requestedProgramId == programId &&
                assessment.TenantId == tenantId && assessment.AssessmentId == assessmentId
                    ? assessment : null);

        public ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
            Uuid tenantId, Uuid requestedProgramId, int? limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            Result<Page<ReadinessAssessmentSummaryView>>.Success(new Page<ReadinessAssessmentSummaryView>(
                requestedProgramId == programId && assessment.TenantId == tenantId
                    ? [new ReadinessAssessmentSummaryView(assessment.AssessmentId,
                        assessment.RuleVersion, assessment.AsOf, assessment.RuleMetCount,
                        assessment.GapCount, assessment.RunBy, assessment.RunAt, null)]
                    : [], null)));

        public ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(Uuid tenantId,
            Uuid requestedProgramId, Uuid assessmentId, string? planState, int? limit,
            string? cursor, Uuid? ownerMemberId, string? kind, string? ruleId,
            string? subject, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
            Uuid tenantId, Uuid requestedProgramId, Uuid assessmentId, int? limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
            Uuid tenantId, Uuid requestedProgramId, int? limit, string? cursor,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

}
