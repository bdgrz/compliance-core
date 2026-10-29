using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldUseBrokerReceiptPrefixGivenMappingPayloadTimesDisagree(bool lateBrokerReceipt)
    {
        // Arrange
        var fixture = await Fixture(includeFutureSuccessor: true, successorBeforePeriod: lateBrokerReceipt,
            capturedControlVersionId: lateBrokerReceipt ? ControlVersionId : FutureControlVersionId);
        var cutoff = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in fixture.Events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString(),
            ControlCriterionMapping.Area), EventCursor.Start, CancellationToken.None))
        {
            var successor = record.Event is ControlCriterionMappingProposed { VersionNumber: 2 } or
                ControlCriterionMappingReviewed { VersionNumber: 2 };
            receipts.Record(record, lateBrokerReceipt && successor ? cutoff.AddMilliseconds(1) : cutoff);
        }
        var context = fixture.Context with
        {
            RecordedThrough = cutoff,
            ReceiptCut = new RecordedSourceReceiptCut(TenantId, cutoff, fixture.Events, receipts),
        };

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(lateBrokerReceipt ? 1 : 2, Assert.Single(result.Value.Sources).Revision);
        Assert.True(receipts.Reads > 0);
    }

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
        var mappingId = Uuid.CreateVersion4();
        var atTheTypeIDate = await Fixture(firstMappingId: mappingId);
        var withFutureSuccessor = await Fixture(includeFutureSuccessor: true, firstMappingId: mappingId);

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
        Uuid? capturedControlVersionId = null, bool successorBeforePeriod = false, Uuid? firstMappingId = null)
    {
        var line = new ControlCriterionMappingRules(TenantId, ProgramId, ControlId, Edition.EditionId,
            Criterion.Identifier);
        var mappingId = firstMappingId ?? Uuid.CreateVersion4();
        Assert.True(line.Propose(MappingRuleFacts.Root(line, mappingId, "ProposeControlCriterionMapping", 0, AuthorId,
                BeforePeriod), ControlVersionId, Criterion.Kind, "Restricts access", "All production systems",
            AuthorId, "Author", BeforePeriod).IsSuccess);
        if (approveInitial)
            Assert.True(line.Review(MappingRuleFacts.Root(line, ReviewDecisionId, "ReviewControlCriterionMapping", 1,
                    ReviewerId, BeforePeriod.AddDays(1)), mappingId, "accept",
                "The mapping matches the selected criterion.", ReviewerId, "Reviewer", BeforePeriod.AddDays(1),
                null).IsSuccess);
        if (includeFutureSuccessor)
        {
            var revision = approveInitial ? 2 : 1;
            var successorAt = successorBeforePeriod ? BeforePeriod.AddDays(4) : AfterPeriod;
            var successorId = Uuid.CreateVersion4();
            Assert.True(line.Propose(MappingRuleFacts.Root(line, successorId, "ProposeControlCriterionMapping", revision,
                    AuthorId, successorAt), FutureControlVersionId, Criterion.Kind, "Maps the successor control",
                "The successor covers production systems", AuthorId, "Author", successorAt).IsSuccess);
            if (approveInitial)
                Assert.True(line.Review(MappingRuleFacts.Root(line, Uuid.CreateVersion4(), "ReviewControlCriterionMapping",
                        revision + 1, ReviewerId, successorAt.AddDays(1)), successorId, "accept",
                    "The successor mapping is approved.", ReviewerId, "Reviewer", successorAt.AddDays(1), null).IsSuccess);
        }

        var events = new InMemoryEventStore();
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        await events.AppendAsync(line.Stream, 0, await MappingNativeSeeds.RaisedAsync(line, content));
        var mappingReader = new ControlMappingReader(new UnusedAggregateReader(), events, content);
        var views = (await mappingReader.ReadProgramAsync(TenantId, ProgramId)).Mappings.ToArray();
        var checkpoint = await CheckpointAfterAsync(events);
        var mappingDirectory = new MappingDirectory(views, checkpoint);
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
        var contributor = new ControlCriterionMappingTypeIBaselineSourceContributor(mappingReader, consistency,
            Catalog);
        return new FixtureState(mappingId, contributor, context, events);
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
        TypeIBaselineSourceReadContext Context, InMemoryEventStore Events);

    sealed class UnusedAggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new NotSupportedException("The baseline read composes mapping lines from committed facts only.");
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
