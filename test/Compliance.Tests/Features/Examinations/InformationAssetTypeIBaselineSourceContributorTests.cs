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

public sealed class InformationAssetTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid AssetId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(AuthorId, "Inventory editor");

    [Fact]
    public async Task ShouldCaptureAssetRevisionAtTypeIDateGivenLaterRevision()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Customer records", "internal", DayOne),
            Revision(2, "Customer records", "restricted", DayOne.AddDays(35))]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("information_assets", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("information_asset_revision", source.Kind);
        Assert.Equal(AssetId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(AssetId, "information_asset_revision_v1:1"),
            source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldForbidRestrictedAssetAtTypeIDateGivenLaterLessRestrictedRevision()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Customer records", "restricted", DayOne),
            Revision(2, "Customer records", "internal", DayOne.AddDays(35))],
            restrictedReadAllowed: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain(AssetId.ToString(), result.Error?.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportRetiredAssetGivenRetirementOnTypeIDate()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Customer records", "confidential", DayOne),
            Revision(2, "Customer records", "confidential", DayOne.AddDays(10),
                TechnologyInventoryRules.Retired)]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, Assert.Single(result.Value.Sources).Revision);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("retired", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Value.Blockers);
    }

    static async Task<FixtureState> Fixture(IReadOnlyList<InformationAssetRevisionRecorded> history,
        bool restrictedReadAllowed = true)
    {
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "technology-inventory",
            AssetId.ToString());
        var attached = history.Select((domainEvent, index) => DomainEventSeed.Attach(
            domainEvent with
            {
                TenantId = TenantId,
                InformationAssetId = AssetId,
                Revision = index + 1,
            }, AssetId, (ulong)index + 1)).ToArray();
        if (attached.Length > 0)
            await events.AppendAsync(stream, 0, attached);
        var checkpoint = await CheckpointAfterAsync(events);
        var source = new AggregateScenario<InformationAsset>(new InformationAsset(TenantId, AssetId))
            .Given(attached).Aggregate;
        var latest = Assert.IsType<InformationAssetRevisionRecorded>(attached.Last());
        var view = new InformationAssetView(TenantId, AssetId, latest.Revision, latest.Content,
            "manual", latest.Actor, latest.ChangedAt);
        var inventory = new TechnologyInventoryReader(view, checkpoint);
        var aggregateReader = new AssetAggregateReader(source);
        var inventoryConsistency = new TechnologyInventoryReadConsistency(inventory,
            aggregateReader, events);
        var entry = new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "information",
            AssetId.ToString(), AssetId, string.Empty, "Included information", false);
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("In-scope information", "type_i",
                ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
                [entry]), "approved", new DateOnly(2026, 6, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaries = new BoundaryDirectory(boundary, version, checkpoint);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(
            new ProgramDirectory(checkpoint), boundaries, events);
        var visibility = new TechnologyInventoryRestrictedVisibility(
            new PermissionAuthorizer(restrictedReadAllowed), new ScopedPermissionAuthorizer());
        var contributor = new InformationAssetTypeIBaselineSourceContributor(aggregateReader,
            events, inventoryConsistency, visibility, boundaries, boundaryConsistency);
        var prior = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
            version.VersionId, version.Revision, SnapshotContentIdentity.ApprovedBoundaryVersion(version));
        return new FixtureState(contributor,
            new TypeIBaselineSourceReadContext(RequesterId, [prior]));
    }

    static InformationAssetRevisionRecorded Revision(long revision, string name,
        string classification, DateTimeOffset changedAt,
        string lifecycle = TechnologyInventoryRules.Active) =>
        InventoryFacts.AssetFact(TenantId, AssetId, revision, new InformationAssetContent(name, classification,
            "RET-1", OwnerId, "Customer information", lifecycle), Actor, changedAt);

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", new DateOnly(2026, 6, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        BoundaryId, [], "draft", 1, DayOne, AuthorId, "Examiner");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(InformationAssetTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class AssetAggregateReader(InformationAsset asset) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is InformationAsset
                ? (TAggregate)(Aggregate)asset
                : aggregate);
    }

    sealed class PermissionAuthorizer(bool restrictedReadAllowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission switch
            {
                RbacPermissions.TechnologyInventoryManage => true,
                RbacPermissions.TechnologyInventoryRestrictedRead => restrictedReadAllowed,
                _ => false,
            });
    }

    sealed class ScopedPermissionAuthorizer : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }

    sealed class TechnologyInventoryReader(InformationAssetView asset,
        ProjectionCheckpoint checkpoint) : ITechnologyInventoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
        public ValueTask<TechnologyComponentView?> GetComponentAsync(Uuid tenantId,
            Uuid componentId, CancellationToken ct = default) =>
            ValueTask.FromResult<TechnologyComponentView?>(null);
        public ValueTask<Page<TechnologyComponentView>> ListComponentsAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));
        public ValueTask<Page<TechnologyComponentView>> ListComponentRevisionsAsync(
            Uuid tenantId, Uuid componentId, int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TechnologyComponentView>([], null));
        public ValueTask<InformationAssetView?> GetAssetAsync(Uuid tenantId, Uuid assetId,
            CancellationToken ct = default) => ValueTask.FromResult<InformationAssetView?>(
            tenantId == asset.TenantId && assetId == asset.InformationAssetId ? asset : null);
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
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);
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
