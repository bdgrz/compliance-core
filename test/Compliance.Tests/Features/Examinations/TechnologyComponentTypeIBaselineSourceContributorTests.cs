using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class TechnologyComponentTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid ComponentId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(AuthorId, "Inventory editor");

    [Fact]
    public async Task ShouldCaptureComponentRevisionAtTypeIDateGivenLaterRevision()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Core VPC", DayOne),
            Revision(2, "Core VPC east", DayOne.AddDays(35))]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("system_components", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("system_component_revision", source.Kind);
        Assert.Equal(ComponentId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(ComponentId, "technology_component_revision_v1:1"),
            source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockCloudAccountComponentGivenReviewedSystemWasNotCaptured()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var fixture = await Fixture([Revision(1, "Production AWS", DayOne,
            CloudAccount(id))], componentId: id);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("reviewed system", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("cloud account", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldCaptureCloudAccountComponentGivenMatchingReviewedSystemSource()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var fixture = await Fixture([Revision(1, "Production AWS", DayOne, CloudAccount(id))],
            componentId: id, includeReviewedSystemSource: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("system_component_revision", Assert.Single(result.Value.Sources).Kind);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldReportRetiredComponentGivenRetirementOnTypeIDate()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Core VPC", DayOne),
            Revision(2, "Core VPC", DayOne.AddDays(10), lifecycle: TechnologyInventoryRules.Retired)]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, Assert.Single(result.Value.Sources).Revision);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("retired", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockComponentGivenNoRevisionByTypeIDate()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Core VPC", DayOne.AddDays(35))]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("by the Type I date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldKeepOriginalComponentGivenBackdatedRevisionCommitsAfterReceiptCut()
    {
        // Arrange
        var cutoff = DayOne.AddDays(20);
        var first = Revision(1, "Original VPC", DayOne);
        var next = Revision(2, "Backdated VPC", DayOne.AddDays(1));
        var fixture = await Fixture([first, next], recordedThrough: cutoff,
            committedTimes: [cutoff.AddMilliseconds(-1), cutoff.AddMilliseconds(1)]);
        var original = await Fixture([first]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);
        var expected = await original.Contributor.ReadAsync(Engagement(), original.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(expected.IsSuccess, expected.Error?.Message);
        Assert.Equal(1, Assert.Single(result.Value.Sources).Revision);
        Assert.Equal(Assert.Single(expected.Value.Sources).ContentSha256, Assert.Single(result.Value.Sources).ContentSha256);
    }

    static async Task<FixtureState> Fixture(IReadOnlyList<TechnologyComponentRevisionRecorded> history,
        Uuid? componentId = null, bool includeReviewedSystemSource = false,
        DateTimeOffset? recordedThrough = null, IReadOnlyList<DateTimeOffset>? committedTimes = null)
    {
        var id = componentId ?? ComponentId;
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "technology-inventory", id.ToString());
        var attached = history.Select((domainEvent, index) => DomainEventSeed.Attach(
            domainEvent with { TenantId = TenantId, ComponentId = id, Revision = index + 1 }, id,
            (ulong)index + 1)).ToArray();
        if (attached.Length > 0)
            await events.AppendAsync(stream, 0, attached);
        var checkpoint = await CheckpointAfterAsync(events);
        var source = new AggregateScenario<TechnologyComponent>(new TechnologyComponent(TenantId, id))
            .Given(attached).Aggregate;
        var latest = Assert.IsType<TechnologyComponentRevisionRecorded>(attached.Last());
        var view = new TechnologyComponentView(TenantId, id, latest.Revision, latest.Content,
            "manual", latest.Actor, latest.ChangedAt);
        var inventory = new TechnologyInventoryReader(view, checkpoint);
        var inventoryConsistency = new TechnologyInventoryReadConsistency(inventory,
            new ComponentAggregateReader(source), events);

        var entry = new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "component",
            id.ToString(), id, string.Empty, "Included component", false);
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("In-scope components", "type_i",
                ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
                [entry]), "approved", new DateOnly(2026, 6, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaries = new BoundaryDirectory(boundary, version, checkpoint);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(
            new ProgramDirectory(checkpoint), boundaries, events);
        var contributor = new TechnologyComponentTypeIBaselineSourceContributor(
            new ComponentAggregateReader(source), events, inventoryConsistency, boundaries,
            boundaryConsistency);
        var prior = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
            version.VersionId, version.Revision, SnapshotContentIdentity.ApprovedBoundaryVersion(version));
        var priorSources = new List<TypeIBaselineSourceReference> { prior };
        if (includeReviewedSystemSource)
            priorSources.Add(new TypeIBaselineSourceReference("reviewed_system_revision", id,
                Uuid.CreateVersion5(id, "reviewed_system_revision_v1:1"), 1, new string('a', 64)));
        var context = new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), priorSources.AsReadOnly());
        if (recordedThrough is { } through)
        {
            var receipts = new ScriptedNativeEventReceipts();
            await foreach (var record in events.ReadAsync(stream, 0, CancellationToken.None))
                receipts.Record(record, committedTimes![checked((int)record.ResourceOffset)]);
            context = context with
            {
                RecordedThrough = through,
                ReceiptCut = new RecordedSourceReceiptCutFactory(events, receipts).Create(TenantId, through)
            };
        }
        return new FixtureState(contributor, context);
    }

    static TechnologyComponentRevisionRecorded Revision(long revision, string name,
        DateTimeOffset changedAt, string lifecycle = TechnologyInventoryRules.Active) =>
        InventoryFacts.ComponentFact(TenantId, ComponentId, revision, new TechnologyComponentContent("network", name,
            OwnerId, null, null, null, null, null, lifecycle), Actor, changedAt);

    static TechnologyComponentRevisionRecorded Revision(long revision, string name,
        DateTimeOffset changedAt, TechnologyComponentContent content) =>
        InventoryFacts.ComponentFact(TenantId, ComponentId, revision, content, Actor, changedAt);

    static TechnologyComponentContent CloudAccount(Uuid id) =>
        new(TechnologyInventoryRules.CloudAccount, "Production AWS", OwnerId, null, null,
            id, null, null, TechnologyInventoryRules.Active);

    static ExaminationEngagementView Engagement(DateOnly? periodStart = null) => new(TenantId,
        ProgramId, Uuid.CreateVersion4(), "type_i", "Audit Firm", periodStart ?? new DateOnly(2026, 6, 30),
        null, ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        BoundaryId, [], "draft", 1, DayOne, AuthorId, "Examiner");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(TechnologyComponentTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class ComponentAggregateReader(TechnologyComponent component) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is TechnologyComponent
                ? (TAggregate)(Aggregate)component
                : aggregate);
    }

    sealed class TechnologyInventoryReader(TechnologyComponentView component,
        ProjectionCheckpoint checkpoint) : ITechnologyInventoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
        public ValueTask<TechnologyComponentView?> GetComponentAsync(Uuid tenantId, Uuid componentId,
            CancellationToken ct = default) => ValueTask.FromResult<TechnologyComponentView?>(
            tenantId == component.TenantId && componentId == component.ComponentId ? component : null);
        public ValueTask<Page<TechnologyComponentView>> ListComponentsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));
        public ValueTask<Page<TechnologyComponentView>> ListComponentRevisionsAsync(Uuid tenantId,
            Uuid componentId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));
        public ValueTask<InformationAssetView?> GetAssetAsync(Uuid tenantId, Uuid assetId,
            CancellationToken ct = default) => ValueTask.FromResult<InformationAssetView?>(null);
        public ValueTask<Page<InformationAssetView>> ListAssetsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<InformationAssetView>([], null));
        public ValueTask<Page<InformationAssetView>> ListAssetRevisionsAsync(Uuid tenantId,
            Uuid assetId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<InformationAssetView>([], null));
        public ValueTask<DataFlowView?> GetFlowAsync(Uuid tenantId, Uuid flowId,
            CancellationToken ct = default) => ValueTask.FromResult<DataFlowView?>(null);
        public ValueTask<Page<DataFlowView>> ListFlowsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<DataFlowView>([], null));
        public ValueTask<Page<DataFlowView>> ListFlowRevisionsAsync(Uuid tenantId, Uuid flowId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<DataFlowView>([], null));
    }

    sealed class BoundaryDirectory(BoundaryView boundary, BoundaryVersionView version,
        ProjectionCheckpoint checkpoint) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(boundary);
        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([boundary], null));
        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(new Page<BoundaryVersionView>([version], null));
        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid decisionId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryDecisionView?>(null);
        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(new Page<BoundaryDecisionView>([], null));
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }

    sealed class ProgramDirectory(ProjectionCheckpoint checkpoint) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProgramView>([], null));
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(new Page<ProgramRevisionView>([], null));
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProgramRevisionView?>(null);
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }
}
