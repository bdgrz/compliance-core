using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class FindingBaselineMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectIncompleteAsOfHistoryGivenLaterIncludedRevisionAfterExcludedRaise(bool native)
    {
        // Arrange
        await using var fixture = new Fixture();
        if (native)
            await fixture.MigrateAsync();
        var target = new FindingAggregate(fixture.Tenant, fixture.Program, fixture.Finding);
        var source = new FindingSource("manual", null, null, "Exact wording");
        var due = new DateOnly(2026, 3, 1);
        DomainEvent raised = native
            ? new FindingRaisedV2(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Finding,
                1, source, "Gap", "Description", "medium", "All", fixture.Member, due, [], fixture.Actor, Fixture.After)
            : new FindingRaised(fixture.Tenant, fixture.Program, fixture.Finding,
                1, source, "Gap", "Description", "medium", "All", fixture.Member, due, [], fixture.Actor, Fixture.After);
        DomainEvent revised = native
            ? new FindingRevisedV2(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Finding,
                2, "high", fixture.Member, due, "Changed", null, "Backdated", fixture.Actor, Fixture.Before)
            : new FindingRevised(fixture.Tenant, fixture.Program, fixture.Finding,
                2, "high", fixture.Member, due, "Changed", null, "Backdated", fixture.Actor, Fixture.Before);
        var aggregateId = native ? target.Id : fixture.Program;
        await fixture.Store.AppendAsync(native ? target.Stream : fixture.Legacy, 0,
        [
            DomainEventSeed.Attach(raised, aggregateId, 1, occurredOn: Fixture.Before.AddSeconds(1)),
            DomainEventSeed.Attach(revised, aggregateId, 2, occurredOn: Fixture.Before.AddSeconds(2)),
        ]);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveExactBaselineIdentityGivenMigrationAfterCutoffAndPhysicalAudits(bool includeAudit)
    {
        // Arrange
        await using var fixture = new Fixture();
        var raised = new FindingRaised(fixture.Tenant, fixture.Program, fixture.Finding, 1,
            new FindingSource("manual", null, null, "Exact wording"), "Gap", "Description", "medium", "All",
            fixture.Member, new DateOnly(2026, 3, 1), [], fixture.Actor, Fixture.Before);
        var source = new List<DomainEvent>
        {
            DomainEventSeed.Attach(raised, fixture.Program, 1,
                eventId: Uuid.CreateVersion5(fixture.Finding, "original-raise"), occurredOn: Fixture.Before.AddSeconds(1)),
        };
        if (includeAudit)
            source.Add(DomainEventSeed.Attach(raised with { }, fixture.Program, 1, isAudit: true,
                occurredOn: Fixture.Before.AddSeconds(2)));
        source.Add(DomainEventSeed.Attach(new FindingRevised(fixture.Tenant, fixture.Program, fixture.Finding,
            2, "high", fixture.Member, new DateOnly(2026, 3, 2), "Later scope", null, "After cutoff",
            fixture.Actor, Fixture.After), fixture.Program, 2, occurredOn: Fixture.After.AddSeconds(1)));
        await fixture.Store.AppendAsync(fixture.Legacy, 0, source.ToArray());
        var before = await fixture.ReadAsync();
        Assert.True(before.IsSuccess, before.Error?.Message);

        // Act
        await fixture.MigrateAsync();
        var migrated = await fixture.ReadAsync();
        var target = new FindingAggregate(fixture.Tenant, fixture.Program, fixture.Finding);
        var targetRecords = new List<DomainEventRecord>();
        await foreach (var record in fixture.Store.ReadAsync(target.Stream, 0))
            targetRecords.Add(record);
        await fixture.Store.AppendAsync(target.Stream, (ulong)targetRecords.Count,
        [
            DomainEventSeed.Attach(new FindingRevisedV2(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
                fixture.Finding, 3, "critical", fixture.Member, new DateOnly(2026, 3, 3), "Native later scope",
                null, "Native after cutoff", fixture.Actor, Fixture.After.AddDays(1)), target.Id,
                (ulong)targetRecords.Count + 1, occurredOn: DateTimeOffset.UtcNow),
        ]);
        var afterNative = await fixture.ReadAsync();

        // Assert
        Assert.True(migrated.IsSuccess, migrated.Error?.Message);
        Assert.True(afterNative.IsSuccess, afterNative.Error?.Message);
        Assert.Equal(Assert.Single(before.Value.Sources), Assert.Single(migrated.Value.Sources));
        Assert.Equal(before.Value.Sources[0], Assert.Single(afterNative.Value.Sources));
        var lineage = await FindingHistoryFold.ReadAsync(fixture.Store, fixture.Tenant,
            fixture.Program, fixture.Finding, CancellationToken.None);
        Assert.Equal(raised.Metadata.EventId, lineage.SourceLineage[0].SourceEventId);
        Assert.Equal(raised.Metadata.OccurredOn, lineage.SourceLineage[0].SourceOccurredAt);
        Assert.Equal(includeAudit ? 2UL : 1UL, lineage.SourceLineage[1].SourceProgramStreamOffset);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task ShouldHonorBothCutoffTimesGivenNativeFinding(bool businessAfter, bool recordedAfter, bool included)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.MigrateAsync();
        var target = new FindingAggregate(fixture.Tenant, fixture.Program, fixture.Finding);
        var fact = new FindingRaisedV2(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Finding,
            1, new FindingSource("manual", null, null, "Exact wording"), "Gap", "Description", "medium", "All",
            fixture.Member, new DateOnly(2026, 3, 1), [], fixture.Actor, businessAfter ? Fixture.After : Fixture.Before);
        await fixture.Store.AppendAsync(target.Stream, 0,
            [DomainEventSeed.Attach(fact, target.Id, 1, occurredOn: recordedAfter ? Fixture.After : Fixture.Before.AddSeconds(1))]);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(included ? 1 : 0, result.Value.Sources.Count);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public static readonly DateTimeOffset Before = new(2026, 1, 30, 12, 0, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset After = new(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public InMemoryEventStore Store { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Finding { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public ActorReference Actor => ActorReference.ForMember(Member, "Lead");
        public EventStreamAddress Legacy => new(Tenant.ToString(), "remediation", Program.ToString());

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Store);
            services.AddSingleton<IDomainEventReader>(Store);
            services.AddSingleton(TimeProvider.System);
            services.AddPortia().AddRequestHandler<MigrateFindingHistoryHandler>()
                .AddRequestAuthorizer<MigrateFindingHistoryAuthorizer>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
        }

        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync() =>
            new FindingTypeIBaselineSourceContributor(Store,
                _scope.ServiceProvider.GetRequiredService<IAggregateReader>()).ReadAsync(
                new ExaminationEngagementView(Tenant, Program, Uuid.CreateVersion4(), "type_i", "Firm",
                    new DateOnly(2026, 1, 31), null, ["security"], Uuid.CreateVersion4(), [], "draft", 1,
                    After, Member, "Lead"), CancellationToken.None);

        public async Task MigrateAsync()
        {
            var request = new MigrateFindingHistory(Tenant, Program, Uuid.CreateVersion4());
            var bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var context = new RequestDispatchContext(RequestActor.CreateSystem("migration:FindingHistoryV1", "bdgrz.system"), new DirectInvocation());
            Assert.True((await bus.DispatchAsync(request, context)).IsSuccess);
            DomainEventRecord? fence = null;
            await foreach (var record in Store.ReadAsync(Legacy, 0))
                if (record.Event is RemediationPartitioningStarted)
                    fence = record;
            Assert.NotNull(fence);
            for (ulong offset = 0; offset <= fence.ResourceOffset; offset++)
            {
                var result = await bus.DispatchAsync(request with { ExpectedSourceOffset = offset }, context);
                Assert.True(result.IsSuccess, result.Error?.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
