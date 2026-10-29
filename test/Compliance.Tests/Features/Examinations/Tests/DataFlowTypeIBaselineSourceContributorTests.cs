using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class DataFlowTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid FlowId = Uuid.CreateVersion4();
    static readonly Uuid SourceComponentId = Uuid.CreateVersion4();
    static readonly Uuid DestinationComponentId = Uuid.CreateVersion4();
    static readonly Uuid AssetId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(AuthorId, "Inventory editor");

    [Fact]
    public async Task ShouldCaptureEffectiveFlowRevisionGivenKnownFutureEffectiveRevision()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Payroll export", DayOne,
                new DateOnly(2026, 6, 1)),
            Revision(2, "Payroll export v2", DayOne.AddDays(10),
                new DateOnly(2026, 7, 1))]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("data_flows", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("data_flow_revision", source.Kind);
        Assert.Equal(FlowId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(FlowId, "data_flow_revision_v1:1"), source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("scheduled after the Type I date", StringComparison.Ordinal));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockFlowGivenCarriedAssetWasNotCaptured()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Payroll export", DayOne,
                new DateOnly(2026, 6, 1))], includeAssetSource: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("information asset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("information asset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldBlockFlowGivenEndpointComponentsWereNotCaptured()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Payroll export", DayOne,
                new DateOnly(2026, 6, 1))], includeComponentSources: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Equal(2, result.Value.Blockers.Count(blocker =>
            blocker.Contains("component", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ShouldForbidRestrictedFlowAtTypeIDateGivenLaterLessRestrictedRevision()
    {
        // Arrange
        var fixture = await Fixture([Revision(1, "Restricted transfer", DayOne,
                new DateOnly(2026, 6, 1), "restricted"),
            Revision(2, "Restricted transfer", DayOne.AddDays(35),
                new DateOnly(2026, 7, 6), "internal")], restrictedReadAllowed: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain(FlowId.ToString(), result.Error?.Message,
            StringComparison.Ordinal);
    }

    static async Task<FixtureState> Fixture(IReadOnlyList<DataFlowRevisionRecorded> history,
        bool includeAssetSource = true, bool includeComponentSources = true,
        bool restrictedReadAllowed = true)
    {
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "technology-inventory",
            FlowId.ToString());
        var attached = history.Select((domainEvent, index) => DomainEventSeed.Attach(
            domainEvent with
            {
                TenantId = TenantId,
                DataFlowId = FlowId,
                Revision = index + 1,
            }, FlowId, (ulong)index + 1)).ToArray();
        if (attached.Length > 0)
            await events.AppendAsync(stream, 0, attached);
        var checkpoint = await CheckpointAfterAsync(events);
        var source = new AggregateScenario<DataFlow>(new DataFlow(TenantId, FlowId))
            .Given(attached).Aggregate;
        var latest = Assert.IsType<DataFlowRevisionRecorded>(attached.Last());
        var view = new DataFlowView(TenantId, FlowId, latest.Revision, latest.Content,
            "manual", latest.Actor, latest.ChangedAt);
        var inventory = new TechnologyInventoryReader(view, checkpoint);
        var aggregateReader = new FlowAggregateReader(source);
        var inventoryConsistency = new TechnologyInventoryReadConsistency(inventory,
            aggregateReader, events);
        var entry = new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "data_flow",
            FlowId.ToString(), FlowId, string.Empty, "Included data flow", false);
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("In-scope data flows", "type_i",
                ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
                [entry]), "approved", new DateOnly(2026, 6, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaries = new BoundaryDirectory(boundary, version, checkpoint);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(
            new ProgramDirectory(checkpoint), boundaries, events);
        var visibility = new TechnologyInventoryRestrictedVisibility(
            new PermissionAuthorizer(restrictedReadAllowed), new ScopedPermissionAuthorizer());
        var contributor = new DataFlowTypeIBaselineSourceContributor(aggregateReader, events,
            inventoryConsistency, visibility, boundaries, boundaryConsistency);
        var priorSources = new List<TypeIBaselineSourceReference>
        {
            new("approved_boundary", BoundaryId, version.VersionId, version.Revision,
                SnapshotContentIdentity.ApprovedBoundaryVersion(version)),
        };
        if (includeComponentSources)
        {
            priorSources.Add(ComponentSource(SourceComponentId));
            priorSources.Add(ComponentSource(DestinationComponentId));
        }
        if (includeAssetSource)
            priorSources.Add(new TypeIBaselineSourceReference("information_asset_revision",
                AssetId, Uuid.CreateVersion5(AssetId, "information_asset_revision_v1:1"), 1,
                new string('a', 64)));
        return new FixtureState(contributor,
            new TypeIBaselineSourceReadContext(RequesterId, priorSources.AsReadOnly()));
    }

    static TypeIBaselineSourceReference ComponentSource(Uuid id) =>
        new("system_component_revision", id,
            Uuid.CreateVersion5(id, "technology_component_revision_v1:1"), 1,
            new string('a', 64));

    static DataFlowRevisionRecorded Revision(long revision, string purpose,
        DateTimeOffset changedAt, DateOnly effectiveFrom, string classification = "internal",
        string lifecycle = TechnologyInventoryRules.Active) =>
        InventoryFacts.FlowFact(TenantId, FlowId, revision, new DataFlowContent(
            TechnologyInventoryRules.ComponentReference, SourceComponentId,
            TechnologyInventoryRules.ComponentReference, DestinationComponentId, null,
            [AssetId], purpose, true, true, null, effectiveFrom, OwnerId, lifecycle,
            classification), Actor, changedAt);

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

    sealed record FixtureState(DataFlowTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class FlowAggregateReader(DataFlow flow) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DataFlow ? (TAggregate)(Aggregate)flow : aggregate);
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

    sealed class TechnologyInventoryReader(DataFlowView flow,
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
            CancellationToken ct = default) => ValueTask.FromResult<InformationAssetView?>(null);
        public ValueTask<Page<InformationAssetView>> ListAssetsAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<InformationAssetView>([], null));
        public ValueTask<Page<InformationAssetView>> ListAssetRevisionsAsync(Uuid tenantId,
            Uuid assetId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<InformationAssetView>([], null));
        public ValueTask<DataFlowView?> GetFlowAsync(Uuid tenantId, Uuid flowId,
            CancellationToken ct = default) => ValueTask.FromResult<DataFlowView?>(
            tenantId == flow.TenantId && flowId == flow.DataFlowId ? flow : null);
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
