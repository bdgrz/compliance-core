using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestNativeHistoryTests
{
    [Fact]
    public async Task ShouldReplayExactHistoricalWeeklyAttemptGivenLaterFailedAttempts()
    {
        // Arrange
        await using var fixture = new Fixture();
        var rules = fixture.WeeklyRules();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var key = WeeklyWorkDigest.DeliveryKeyFor(rules.Id, attempt);
            Assert.True(rules.RequestDelivery(rules.Revision, attempt, key, fixture.Worker,
                fixture.At.AddMinutes(attempt * 2)).IsSuccess);
            Assert.True(rules.RecordDeliveryResult(rules.Revision, attempt, key, "failed", "provider_busy",
                fixture.Worker, fixture.At.AddMinutes(attempt * 2 + 1)).IsSuccess);
        }
        await WorkDigestFacts.WeeklyAsync(fixture.Provider, fixture.Tenant, rules.Id, rules);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.Tenant, rules.Id, selectedAttempt: 1));
        var key1 = WeeklyWorkDigest.DeliveryKeyFor(rules.Id, 1);

        // Act
        var requested = owner.RequestDelivery(1, 1, key1, fixture.Worker, fixture.At.AddDays(1));
        var recorded = owner.RecordDeliveryResult(2, 1, key1, "failed", "provider_busy", fixture.Worker,
            fixture.At.AddMinutes(3));
        var changed = owner.RecordDeliveryResult(2, 1, key1, "failed", "another_failure", fixture.Worker,
            fixture.At.AddMinutes(3));
        var history = await fixture.Histories.ReadWeeklyAsync(fixture.Tenant, rules.Id);

        // Assert
        Assert.True(requested.IsSuccess);
        Assert.True(recorded.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(9, owner.Revision);
        Assert.Equal(1, owner.SelectedAttempt?.Number);
        Assert.Equal(4, owner.LatestAttempt?.Number);
        Assert.Equal(4, history.View.DeliveryAttempts.Count);
    }

    [Fact]
    public async Task ShouldPreserveMultiplePendingMemberWeeksGivenDifferentFrozenTimeZones()
    {
        // Arrange
        await using var fixture = new Fixture();
        var rules = new WorkDigestDispatchRules(fixture.Tenant, fixture.Member);
        var firstWeek = new DateOnly(2026, 10, 5);
        var firstAt = new DateTimeOffset(2026, 10, 5, 19, 0, 0, TimeSpan.Zero);
        Assert.True(rules.Schedule(new WorkDigestScheduleWindow(firstWeek, "Pacific/Honolulu", firstAt),
            Uuid.CreateVersion4(), TimeSpan.FromDays(8), firstAt));
        Assert.True(rules.StartAttempt(firstWeek, firstAt));
        Assert.True(rules.RecordUnknown(firstWeek, firstAt.AddMinutes(1), "ambiguous"));
        await WorkDigestFacts.AppendAsync(fixture.Provider, new WorkDigestDispatch(fixture.Tenant, fixture.Member), rules.PendingEvents);
        var captured = await fixture.Reader.HydrateAsync(new WorkDigestDispatch(fixture.Tenant, fixture.Member, firstWeek));
        var nextAt = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var nextWeek = new DateOnly(2026, 10, 12);
        Assert.True(rules.Schedule(new WorkDigestScheduleWindow(nextWeek, "UTC", nextAt), Uuid.CreateVersion4(),
            TimeSpan.FromDays(1), nextAt));
        Assert.True(rules.AuthorizeUnknownRetry(firstWeek, Uuid.CreateVersion4(), "receipt:one", "Confirmed no acceptance",
            nextAt.AddMinutes(1), maximumAttempts: 3));
        await WorkDigestFacts.AppendAsync(fixture.Provider, new WorkDigestDispatch(fixture.Tenant, fixture.Member), rules.PendingEvents);

        // Act
        var capturedHistory = await WorkDigestDispatchHistory.ReadAsync(fixture.Events, captured);
        var current = await fixture.Reader.HydrateAsync(new WorkDigestDispatch(fixture.Tenant, fixture.Member, firstWeek));
        var history = await fixture.Histories.ReadDispatchAsync(fixture.Tenant, fixture.Member);
        var claimed = current.StartAttempt(firstWeek, nextAt.AddMinutes(2));

        // Assert
        Assert.Equal(3, capturedHistory.Facts.Count);
        Assert.Equal(WorkDigestDispatch.Unknown, capturedHistory.Read(firstWeek).Status);
        Assert.Equal(5, history.Facts.Count);
        Assert.Equal(firstWeek, history.FindPending()?.WeekOf);
        Assert.Equal(WorkDigestDispatch.Scheduled, history.Read(nextWeek).Status);
        Assert.True(claimed);
        Assert.Equal(2, current.Read(firstWeek).Attempts);
        Assert.Equal(WorkDigestDispatch.Scheduled, current.Read(nextWeek).Status);
    }

    [Fact]
    public async Task ShouldAllowRepeatedOperatorEvidenceGivenAnotherUnknownAttemptInSameWeek()
    {
        // Arrange
        await using var fixture = new Fixture();
        var rules = new WorkDigestDispatchRules(fixture.Tenant, fixture.Member);
        Assert.True(rules.Schedule(new WorkDigestScheduleWindow(fixture.Week, "UTC", fixture.At),
            Uuid.CreateVersion4(), TimeSpan.FromDays(1), fixture.At));
        var actor = Uuid.CreateVersion4();
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            Assert.True(rules.StartAttempt(fixture.Week, fixture.At.AddMinutes(attempt * 3)));
            Assert.True(rules.RecordUnknown(fixture.Week, fixture.At.AddMinutes(attempt * 3 + 1), "ambiguous"));
            Assert.True(rules.AuthorizeUnknownRetry(fixture.Week, actor, "receipt:same", "Same verified external evidence",
                fixture.At.AddMinutes(attempt * 3 + 2), maximumAttempts: 4));
        }
        await WorkDigestFacts.AppendAsync(fixture.Provider, new WorkDigestDispatch(fixture.Tenant, fixture.Member), rules.PendingEvents);

        // Act
        var owner = await fixture.Reader.HydrateAsync(new WorkDigestDispatch(fixture.Tenant, fixture.Member, fixture.Week));
        var history = await fixture.Histories.ReadDispatchAsync(fixture.Tenant, fixture.Member);

        // Assert
        Assert.Equal(WorkDigestDispatch.RetryPending, owner.Read(fixture.Week).Status);
        Assert.Equal(2, owner.Read(fixture.Week).Attempts);
        Assert.Equal(2, history.Facts.OfType<WorkDigestUnknownRetryAuthorized>().Count());
        Assert.True(owner.StartAttempt(fixture.Week, fixture.At.AddMinutes(9)));
        Assert.Equal(3, owner.Read(fixture.Week).Attempts);
    }

    [Theory]
    [InlineData(false, "missing", true)]
    [InlineData(true, "missing", true)]
    [InlineData(false, "foreign_stream", false)]
    [InlineData(true, "foreign_stream", false)]
    [InlineData(false, "physical_gap", false)]
    [InlineData(true, "physical_gap", false)]
    [InlineData(false, "historical_payload", false)]
    [InlineData(true, "historical_payload", false)]
    public async Task ShouldRefuseContradictoryNativeHistoryGivenCapturedDigestCut(bool weekly, string fault, bool transient)
    {
        // Arrange
        await using var fixture = new Fixture();
        Aggregate owner;
        if (weekly)
        {
            var rules = fixture.WeeklyRules();
            Assert.True(rules.RequestDelivery(1, 1, WeeklyWorkDigest.DeliveryKeyFor(rules.Id, 1), fixture.Worker,
                fixture.At.AddMinutes(1)).IsSuccess);
            Assert.True(rules.RecordDeliveryResult(2, 1, WeeklyWorkDigest.DeliveryKeyFor(rules.Id, 1), "failed", "provider_busy",
                fixture.Worker, fixture.At.AddMinutes(2)).IsSuccess);
            Assert.True(rules.RequestDelivery(3, 2, WeeklyWorkDigest.DeliveryKeyFor(rules.Id, 2), fixture.Worker,
                fixture.At.AddMinutes(3)).IsSuccess);
            await WorkDigestFacts.WeeklyAsync(fixture.Provider, fixture.Tenant, rules.Id, rules);
            owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.Tenant, rules.Id));
        }
        else
        {
            await WorkDigestFacts.DispatchAsync(fixture.Provider, fixture.Tenant, fixture.Member, rules =>
                rules.Schedule(new WorkDigestScheduleWindow(fixture.Week, "UTC", fixture.At), Uuid.CreateVersion4(),
                    TimeSpan.FromDays(1), fixture.At) && rules.StartAttempt(fixture.Week, fixture.At.AddMinutes(1)));
            owner = await fixture.Reader.HydrateAsync(new WorkDigestDispatch(fixture.Tenant, fixture.Member));
        }
        var raw = new AlteredEvents(fixture.Events, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with { Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), record.Stream.Area, record.Stream.Resource) },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "historical_payload" when record.Event is WorkDigestDispatchScheduled scheduled => record with { Event = Changed(scheduled) },
            "historical_payload" when record.Event is WeeklyWorkDigestDeliveryRecorded recorded => record with { Event = Changed(recorded) },
            _ => record
        });

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (owner is WeeklyWorkDigest digest)
                _ = await WeeklyWorkDigestHistory.ReadAsync(raw, digest);
            else
                _ = await WorkDigestDispatchHistory.ReadAsync(raw, (WorkDigestDispatch)owner);
        });

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);
    }

    static WorkDigestDispatchScheduled Changed(WorkDigestDispatchScheduled fact)
    {
        var changed = fact with { RecordedAt = fact.RecordedAt.AddMinutes(-1) };
        changed.AttachMetadata(fact.Metadata);
        return changed;
    }
    static WeeklyWorkDigestDeliveryRecorded Changed(WeeklyWorkDigestDeliveryRecorded fact)
    {
        var changed = fact with { FailureCode = "another_valid_failure_code" };
        changed.AttachMetadata(fact.Metadata);
        return changed;
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly AsyncServiceScope _scope;
        public ServiceProvider Provider { get; }
        public InMemoryEventStore Events { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public DateOnly Week { get; } = new(2026, 10, 12);
        public DateTimeOffset At { get; } = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public ActorReference Worker { get; } = ActorReference.ForSystemProcess(WeeklyWorkDigest.DeliveryProcessId, "Worker");
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public WorkDigestHistoryReader Histories => new(Reader, Events);
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddPortia();
            services.AddVerifiedWorkDigestReader();
            Provider = services.BuildServiceProvider();
            _scope = Provider.CreateAsyncScope();
        }
        public WeeklyWorkDigestRules WeeklyRules()
        {
            var rules = new WeeklyWorkDigestRules(Tenant, WeeklyWorkDigest.IdFor(Tenant, Member, Week));
            Assert.True(rules.Schedule(Member, Week, "actual-schedule", ActorReference.ForSystemProcess(
                WeeklyWorkDigest.SchedulerProcessId, "Scheduler"), At).IsSuccess);
            return rules;
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }
    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
