using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ControlCriterionMappingTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid FutureControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewDecisionId = Uuid.CreateVersion4();
    static readonly DateTimeOffset BeforePeriod = new(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AfterPeriod = new(2026, 2, 2, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly PeriodStart = new(2026, 1, 31);
    static readonly CriteriaCatalog Catalog = CriteriaCatalog.Platform;
    static readonly CriteriaCatalogEdition Edition = Catalog.Edition;
    static readonly Criterion Criterion = Catalog.ListEntries(Edition.EditionId, null,
        "criterion", null)[0];

    [Fact]
    public async Task ShouldCaptureMappingEffectiveOnTypeIDateGivenLaterApprovedSuccessor()
    {
        // Arrange
        var fixture = await Fixture(includeFutureSuccessor: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("control_criterion_mappings", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("control_criterion_mapping_version", source.Kind);
        Assert.Equal(fixture.MappingId, source.RecordId);
        Assert.Equal(Uuid.CreateVersion5(fixture.MappingId,
            "control_criterion_mapping_version_v1:1"), source.VersionId);
        Assert.Equal(1, source.Revision);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldKeepMappingDigestStableGivenSuccessorApprovedAfterTypeIDate()
    {
        // Arrange
        var atTheTypeIDate = await Fixture();
        var withFutureSuccessor = await Fixture(includeFutureSuccessor: true);

        // Act
        var original = await atTheTypeIDate.Contributor.ReadAsync(Engagement(),
            atTheTypeIDate.Context, CancellationToken.None);
        var later = await withFutureSuccessor.Contributor.ReadAsync(Engagement(),
            withFutureSuccessor.Context, CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        var originalSource = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(later.Value.Sources);
        Assert.Equal(originalSource.VersionId, laterSource.VersionId);
        Assert.Equal(originalSource.ContentSha256, laterSource.ContentSha256);
    }

    [Fact]
    public async Task ShouldUseOneSourceReferenceGivenMultipleMappingVersionsByTypeIDate()
    {
        // Arrange
        var fixture = await Fixture(includeFutureSuccessor: true, successorBeforePeriod: true,
            capturedControlVersionId: FutureControlVersionId);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal(Uuid.CreateVersion5(fixture.MappingId,
            "control_criterion_mapping_version_v1:2"), source.VersionId);
        Assert.Equal(2, source.Revision);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCapturePendingMappingGivenProposalBeforeTypeIDate()
    {
        // Arrange
        var fixture = await Fixture(approveInitial: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("control_criterion_mapping_version", source.Kind);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("pending", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockAcceptedMappingGivenControlVersionOutsideTypeIBaseline()
    {
        // Arrange
        var fixture = await Fixture(includeControlSource: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("control source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldFlagRemappingGivenAcceptedMappingReferencesOlderControlVersion()
    {
        // Arrange
        var fixture = await Fixture(capturedControlVersionId: FutureControlVersionId);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Blockers);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("requires remapping", StringComparison.OrdinalIgnoreCase));
    }

    static async Task<FixtureState> Fixture(bool includeFutureSuccessor = false,
        bool approveInitial = true, bool includeControlSource = true,
        Uuid? capturedControlVersionId = null, bool successorBeforePeriod = false)
    {
        var ledger = new ControlCriterionMappingLedger(TenantId, ProgramId);
        Assert.Null(ledger.Propose(ControlId, ControlVersionId, Edition.EditionId,
            Criterion.Identifier, Criterion.Kind, 0, "Restricts access",
            "All production systems", AuthorId, "Author", BeforePeriod,
            out var registration));
        var mappingId = Assert.IsType<ControlCriterionMappingRegistration>(registration).MappingId;
        if (approveInitial)
            Assert.Null(ledger.Review(mappingId, 1, ReviewDecisionId, "accept",
                "The mapping matches the selected criterion.", ReviewerId, "Reviewer",
                BeforePeriod.AddDays(1)));
        if (includeFutureSuccessor)
        {
            var revision = approveInitial ? 2 : 1;
            var successorAt = successorBeforePeriod ? BeforePeriod.AddDays(4) : AfterPeriod;
            Assert.Null(ledger.Propose(ControlId, FutureControlVersionId, Edition.EditionId,
                Criterion.Identifier, Criterion.Kind, revision, "Maps the successor control",
                "The successor covers production systems", AuthorId, "Author", successorAt,
                out _));
            if (approveInitial)
                Assert.Null(ledger.Review(mappingId, revision + 1, Uuid.CreateVersion4(), "accept",
                    "The successor mapping is approved.", ReviewerId, "Reviewer",
                    successorAt.AddDays(1)));
        }

        var pending = new AggregateScenario<ControlCriterionMappingLedger>(ledger).PendingEvents;
        var stream = new EventStreamAddress(TenantId.ToString(), "control-criterion-mappings",
            ProgramId.ToString());
        var attached = pending.ToArray();
        var events = new InMemoryEventStore();
        await events.AppendAsync(stream, 0, attached);
        var sourceLedger = new AggregateScenario<ControlCriterionMappingLedger>(
            new ControlCriterionMappingLedger(TenantId, ProgramId)).Given(attached).Aggregate;
        var checkpoint = await CheckpointAfterAsync(events);
        var mappingDirectory = new MappingDirectory(sourceLedger.ReadAll().ToArray(), checkpoint);
        var consistency = new CriteriaCoverageReadConsistency(mappingDirectory,
            new ApplicabilityDirectory(checkpoint), events);
        var references = new List<TypeIBaselineSourceReference>
        {
            new("criteria_selection", ProgramId, Edition.EditionId, 1,
                new string('c', 64)),
            new("criteria_edition", Edition.EditionId, Edition.EditionId, 1,
                new string('a', 64)),
        };
        if (includeControlSource)
            references.Add(new TypeIBaselineSourceReference("control_version", ControlId,
                capturedControlVersionId ?? ControlVersionId, 1, new string('b', 64)));
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, references);
        var contributor = new ControlCriterionMappingTypeIBaselineSourceContributor(
            new MappingAggregateReader(sourceLedger), consistency, Catalog);
        return new FixtureState(mappingId, contributor, context);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", PeriodStart, null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, BeforePeriod, AuthorId, "Examiner");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), "control-criterion-mappings"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(Uuid MappingId,
        ControlCriterionMappingTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class MappingAggregateReader(ControlCriterionMappingLedger source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(source is TAggregate typed ? typed : aggregate);
    }

    sealed class MappingDirectory(ControlCriterionMappingView[] mappings,
        ProjectionCheckpoint checkpoint) : IControlMappingDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Page<ControlCriterionMappingView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ControlCriterionMappingView>(mappings, null));
    }

    sealed class ApplicabilityDirectory(ProjectionCheckpoint checkpoint)
        : ICriterionApplicabilityDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Page<CriterionApplicabilityView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CriterionApplicabilityView>([], null));
    }
}
