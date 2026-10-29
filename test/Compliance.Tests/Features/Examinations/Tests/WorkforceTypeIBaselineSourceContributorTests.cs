using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Snapshots.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class WorkforceTypeIBaselineSourceContributorTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(),
        "Workforce owner");

    [Fact]
    public async Task ShouldSelectLatestRosterSnapshotOnOrBeforeTypeIDateGivenSnapshotHistory()
    {
        // Arrange
        var engagement = Engagement();
        var before = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 1, 12, 0, 0,
            TimeSpan.Zero));
        var selected = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 30, 20, 0, 0,
            TimeSpan.Zero), before.Summary.SnapshotId, before.Summary.RootSnapshotId);
        var after = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 10, 1, 12, 0, 0,
            TimeSpan.Zero));
        var reader = new SnapshotAggregateReader(before.Source, selected.Source, after.Source);
        var directory = new SnapshotDirectory(before.Summary, selected.Summary, after.Summary);
        var contributor = Contributor(directory, reader, new InMemoryEventStore());

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("workforce_roster_snapshot", source.Kind);
        Assert.Equal(selected.Summary.RootSnapshotId, source.RecordId);
        Assert.Equal(selected.Summary.SnapshotId, source.VersionId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(selected.Summary.ContentSha256, source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockBaselineGivenNoWorkforceSnapshotBeforeTypeIDate()
    {
        // Arrange
        var engagement = Engagement();
        var future = Snapshot(engagement.TenantId, Now);
        var contributor = Contributor(new SnapshotDirectory(future.Summary),
            new SnapshotAggregateReader(future.Source), new InMemoryEventStore());

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("workforce roster",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenUnprojectedWorkforceSnapshot()
    {
        // Arrange
        var engagement = Engagement();
        var snapshot = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 29, 12, 0, 0,
            TimeSpan.Zero));
        var store = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IAggregateWriter>().SaveAsync(snapshot.Source,
            new RequestDispatchContext(RequestActor.System));
        var directory = new SnapshotDirectory(snapshot.Summary);
        var consistency = new PopulationSnapshotDirectoryReadConsistency(directory,
            provider.GetRequiredService<IDomainEventReader>());
        var contributor = new WorkforceTypeIBaselineSourceContributor(directory,
            new SnapshotAggregateReader(snapshot.Source), consistency);

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenSnapshotProjectionAdvancesDuringRead()
    {
        // Arrange
        var engagement = Engagement();
        var snapshot = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 29, 12, 0, 0,
            TimeSpan.Zero));
        var directory = new AdvancingSnapshotDirectory(snapshot.Summary);
        var contributor = Contributor(directory, new SnapshotAggregateReader(snapshot.Source),
            new InMemoryEventStore());

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldBlockBaselineGivenSnapshotDigestDiffersFromImmutableContent()
    {
        // Arrange
        var engagement = Engagement();
        var snapshot = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 29, 12, 0, 0,
            TimeSpan.Zero));
        var tampered = snapshot.Summary with { ContentSha256 = new string('a', 64) };
        var contributor = Contributor(new SnapshotDirectory(tampered),
            new SnapshotAggregateReader(snapshot.Source), new InMemoryEventStore());

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("integrity",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldSelectEarlierIntactRosterGivenLaterFreezeCommitsAfterReceiptCut()
    {
        // Arrange
        var engagement = Engagement();
        var earlier = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var later = Snapshot(engagement.TenantId, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var events = new InMemoryEventStore();
        var receipts = new ScriptedNativeEventReceipts();
        var through = Now;
        foreach (var pair in new[] { earlier, later })
        {
            var fact = DomainEventSeed.Attach(pair.Source.FrozenFact! with { }, pair.Source.Id, 1);
            await events.AppendAsync(pair.Source.Stream, 0, [fact]);
            await foreach (var record in events.ReadAsync(pair.Source.Stream, 0, CancellationToken.None))
                receipts.Record(record, pair.Source.Id == later.Source.Id ? through.AddMilliseconds(1) : through.AddMilliseconds(-1));
        }
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(engagement.TenantId.ToString(), "population-snapshots"), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        var directory = new SnapshotDirectory(earlier.Summary, later.Summary) { Checkpoint = new(cursor) };
        ITypeIBaselineSourceContributor contributor = Contributor(directory, new SnapshotAggregateReader(earlier.Source, later.Source), events);
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [])
        {
            RecordedThrough = through,
            ReceiptCut = new RecordedSourceReceiptCutFactory(events, receipts).Create(engagement.TenantId, through)
        };

        // Act
        var result = await contributor.ReadAsync(engagement, context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(earlier.Source.Id, Assert.Single(result.Value.Sources).VersionId);
        Assert.Empty(result.Value.Blockers);
    }

    static WorkforceTypeIBaselineSourceContributor Contributor(
        IPopulationSnapshotDirectoryReader directory,
        IAggregateReader reader, IDomainEventReader events) =>
        new(directory, reader, new PopulationSnapshotDirectoryReadConsistency(directory, events));

    static ExaminationEngagementView Engagement() => new(Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "type_i", "Audit Firm",
        new DateOnly(2026, 9, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, Now, Uuid.CreateVersion4(), "Examiner");

    static (PopulationSnapshot Source, PopulationSnapshotSummary Summary) Snapshot(Uuid tenantId,
        DateTimeOffset frozenAt, Uuid? amendsSnapshotId = null, Uuid? rootSnapshotId = null)
    {
        var id = Uuid.CreateVersion4();
        var root = rootSnapshotId ?? id;
        var amendmentReason = amendsSnapshotId is null ? null : "Roster correction";
        var digest = PopulationContentIdentity.Compute("workforce_roster", []).Value;
        var snapshot = new PopulationSnapshot(tenantId, id);
        Assert.True(snapshot.Freeze(root, amendsSnapshotId, "workforce_roster", [], digest.Sha256,
            amendmentReason, Author, frozenAt).IsSuccess);
        return (snapshot, new PopulationSnapshotSummary(tenantId, id, root, amendsSnapshotId,
            "workforce_roster", 0, digest.Sha256, amendmentReason, Author, frozenAt));
    }

    sealed class SnapshotDirectory(params PopulationSnapshotSummary[] snapshots)
        : IPopulationSnapshotDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; init; } = ProjectionCheckpoint.Start;
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind,
            int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(
            new Page<PopulationSnapshotSummary>(snapshots.Where(snapshot =>
                    snapshot.TenantId == tenantId && snapshot.Kind == kind)
                .OrderByDescending(static snapshot => snapshot.FrozenAt)
                .ThenBy(static snapshot => snapshot.SnapshotId.ToString(), StringComparer.Ordinal)
                .ToArray(), null));
    }

    sealed class AdvancingSnapshotDirectory(PopulationSnapshotSummary snapshot)
        : IPopulationSnapshotDirectoryReader
    {
        int _loads;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(++_loads == 1
            ? ProjectionCheckpoint.Start
            : new ProjectionCheckpoint(new EventCursor("advanced")));

        public ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind,
            int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(
            new Page<PopulationSnapshotSummary>([snapshot], null));
    }

    sealed class SnapshotAggregateReader(params PopulationSnapshot[] snapshots) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(snapshots.FirstOrDefault(snapshot => snapshot.Id == aggregate.Id)
                is TAggregate found ? found : aggregate);
    }
}
