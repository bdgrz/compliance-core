using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.SourceUse;

public sealed class SourceUseReservationsVerifiedHistoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRejectUnselectedHistoricalDuplicateGivenVerifiedOwnerRead(bool lateGrant)
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Settled(stream, key), Granted(stream, Key(stream)),
            lateGrant ? Granted(stream, key) : Settled(stream, key));
        var reader = Reader(stream, facts);

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.HydrateAsync(new ReservationOwner(stream)));

        // Assert
        Assert.Contains(lateGrant ? "never after its fence" : "settled at most once", failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnOriginalGrantAndActualTerminalMetadataGivenSelectedClosedHistory()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var fence = Key(stream);
        var live = Key(stream);
        var facts = Attach(stream, Granted(stream, key), Granted(stream, live), Settled(stream, key),
            Settled(stream, fence));

        // Act
        var owner = await Reader(stream, facts).HydrateAsync(new ReservationOwner(stream).ForSourceUses(key, fence));
        var original = owner.Uses.GrantOf(key)!;
        var closed = owner.Uses.Find(key)!;
        var fenced = owner.Uses.Find(fence)!;

        // Assert
        Assert.Equal(NativeFactReference.Of(stream, facts[0]), original.GrantEvent);
        Assert.Equal(1UL, original.ReservationGeneration);
        Assert.Equal(facts[0].Metadata, closed.GrantFact!.Metadata);
        Assert.Equal(facts[2].Metadata, closed.SettleFact!.Metadata);
        Assert.True(fenced.IsFence);
        Assert.Equal(facts[3].Metadata, fenced.SettleFact!.Metadata);
        Assert.Null(owner.Uses.GrantOf(fence));
        Assert.True(owner.Uses.HasActiveUse);
        Assert.Equal(2UL, Assert.Single(owner.Uses.ActiveUses).ReservationGeneration);
        Assert.Equal(2UL, owner.Uses.Generation);
    }

    [Fact]
    public async Task ShouldKeepFullKeyScopeAndProveSelectedAbsenceGivenSameRootAtOtherFirstOwner()
    {
        // Arrange
        var stream = Stream();
        var first = Key(stream);
        var other = first with { FirstOwner = Stream() };
        var absent = first with { SourceGeneration = 9 };
        var facts = Attach(stream, Granted(stream, first), Granted(stream, other), Settled(stream, first));

        // Act
        var owner = await Reader(stream, facts).HydrateAsync(new ReservationOwner(stream).ForSourceUses(first, absent));

        // Assert
        Assert.True(owner.Uses.Find(first)!.IsSettled);
        Assert.True(owner.Uses.Find(other)!.IsActive);
        Assert.Null(owner.Uses.Find(absent));
        Assert.Throws<InvalidOperationException>(() => owner.Uses.Find(first with { Purpose = "another_purpose" }));
        Assert.Throws<ArgumentException>(() => owner.Uses.Find(first with { Target = Stream() }));
    }

    [Fact]
    public async Task ShouldRefuseAbsenceAndAppendGivenTruncatedHistoricalCut()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Granted(stream, key), Settled(stream, key));
        var owner = new ReservationOwner(stream).ForSourceUses(key);
        var reader = new SourceUseAggregateReader(new ReplayReader(facts), new Records(stream, facts[..1]));

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentUnavailableException>(async () =>
            await reader.HydrateAsync(owner));

        // Assert
        Assert.Contains("exact hydrated history cut", failure.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => owner.Grant(key));
    }

    [Fact]
    public async Task ShouldRejectSubstitutedClosedPayloadGivenSameCountsPositionsAndEventIdentity()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Granted(stream, key), Settled(stream, key));
        var changed = (FirmStaffProfessionalDutyUseSettled)facts[1] with { Intent = NativeFacts.Ref() };
        changed.AttachMetadata(facts[1].Metadata);
        var reader = new SourceUseAggregateReader(new ReplayReader(facts), new Records(stream, [facts[0], changed]));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.HydrateAsync(new ReservationOwner(stream)));

        // Assert
        Assert.Contains("verified exact native history", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRetainBoundedReturnedStateGivenLongVerifiedHistoryAndOneSelectedFence()
    {
        // Arrange
        var stream = Stream();
        var fence = Key(stream);
        var values = new List<DomainEvent> { Settled(stream, fence) };
        for (var index = 0; index < 128; index++)
        {
            var key = Key(stream);
            values.Add(Granted(stream, key));
            values.Add(Settled(stream, key));
        }
        var facts = Attach(stream, values.ToArray());

        // Act
        var owner = await Reader(stream, facts).HydrateAsync(new ReservationOwner(stream).ForSourceUses(fence));
        var retained = typeof(SourceUseReservations).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner.Uses) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.True(owner.Uses.Find(fence)!.IsFence);
        Assert.False(owner.Uses.HasActiveUse);
        Assert.Equal(128UL, owner.Uses.Generation);
    }

    [Fact]
    public async Task ShouldRefuseLateGrantGivenSelectedFenceAtFinalizedAppendCut()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Settled(stream, key));
        var owner = await Reader(stream, facts).HydrateAsync(new ReservationOwner(stream).ForSourceUses(key));

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() => owner.Grant(key));

        // Assert
        Assert.Contains("never after its fence", failure.Message, StringComparison.Ordinal);
        Assert.Empty(new AggregateScenario<ReservationOwner>(owner).PendingEvents);
    }

    [Fact]
    public async Task ShouldProveLocalAbsenceWithoutSecondReadGivenEmptyOwner()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var records = new Records(stream, []);
        var reader = new SourceUseAggregateReader(new ReplayReader([]), records);

        // Act
        var owner = await reader.HydrateAsync(new ReservationOwner(stream).ForSourceUses(key));
        var granted = owner.Grant(key);

        // Assert
        Assert.True(granted.IsSuccess);
        Assert.Equal(0, records.Reads);
        Assert.Equal(1UL, owner.Uses.Generation);
    }

    [Fact]
    public async Task ShouldUseFinalizedReaderInsideExecutorGivenProductionRegistrationAndSelectedHistory()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        var reader = host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var key = Key(new FirmStaffRecord(staff.StaffMemberId).Stream);
        var operation = Operation(key.Target, key);
        var context = new RequestDispatchContext(RequestActor.System);

        // Act
        var granted = await executor.ExecuteAsync(new FirmStaffRecord(staff.StaffMemberId).ForSourceUses(key),
            owner => AggregateOutcome.Commit(owner.AuthorizeProfessionalDutyUse(operation,
                NativeFacts.Ref(SourceUseAuthorizationIntent.Schema), (long)key.SourceGeneration)), context);
        var settled = await executor.ExecuteAsync(new FirmStaffRecord(staff.StaffMemberId).ForSourceUses(key),
            owner => AggregateOutcome.Commit(owner.SettleProfessionalDutyUse(operation with { LogicalId = Uuid.CreateVersion4() },
                NativeFacts.Ref(SourceUseAuthorizationIntent.Schema), (long)key.SourceGeneration)), context);
        var selected = await reader.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId).ForSourceUses(key));

        // Assert
        Assert.IsType<AggregateExecutor>(executor);
        Assert.True(granted.IsSuccess, granted.Error?.Message);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(selected.Uses.Find(key)!.IsSettled);
        Assert.NotNull(selected.Uses.GrantOf(key));
    }

    [Fact]
    public async Task ShouldKeepPhysicalAuditSessionApartGivenSameOwnerAggregateMetadata()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Granted(stream, key), Settled(stream, key));
        var store = new InMemoryEventStore();
        await store.AppendAsync(stream, 0, facts, CancellationToken.None);
        var audit = Settled(stream, Key(stream));
        audit.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), facts[0].Metadata.AggregateId,
            facts[1].Metadata.AggregateVersion, NativeFacts.Now, IsAudit: true));
        var auditStream = new EventStreamAddress(stream.Realm, stream.Area, Uuid.CreateVersion4().ToString());
        await store.AppendAsync(auditStream, 0, [audit], CancellationToken.None);
        var reader = new SourceUseAggregateReader(new ReplayReader(facts), store);

        // Act
        var owner = await reader.HydrateAsync(new ReservationOwner(stream).ForSourceUses(key));
        var auditRecords = new List<DomainEventRecord>();
        await foreach (var record in store.ReadAsync(auditStream, 0, CancellationToken.None))
            auditRecords.Add(record);

        // Assert
        Assert.Equal(0UL, Assert.Single(auditRecords).ResourceOffset);
        Assert.Equal(2UL, audit.Metadata.AggregateVersion);
        Assert.Equal(2UL, owner.CommittedStreamPosition);
        Assert.Equal(2UL, owner.Version);
        Assert.Equal(1UL, owner.Uses.Generation);
        Assert.Equal(facts[1].Metadata, owner.Uses.Find(key)!.SettleFact!.Metadata);
        Assert.True(audit.Metadata.IsAudit);
        Assert.NotEqual(auditStream, stream);
    }

    [Fact]
    public async Task ShouldRejectSelectedMetadataSubstitutionGivenUnchangedNativePayloadAndIdentity()
    {
        // Arrange
        var stream = Stream();
        var key = Key(stream);
        var facts = Attach(stream, Granted(stream, key), Settled(stream, key));
        var changed = (FirmStaffProfessionalDutyUseSettled)facts[1] with { };
        changed.AttachMetadata(facts[1].Metadata with { CorrelationId = Uuid.CreateVersion4() });
        var reader = new SourceUseAggregateReader(new ReplayReader(facts), new Records(stream, [facts[0], changed]));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.HydrateAsync(new ReservationOwner(stream).ForSourceUses(key)));

        // Assert
        Assert.Contains("actual replayed native metadata", failure.Message, StringComparison.Ordinal);
    }

    static EventStreamAddress Stream() => new("bdgrz", FirmStaffRecord.Area, Uuid.CreateVersion4().ToString());
    static ReservationKey Key(EventStreamAddress stream) => new(Uuid.CreateVersion4(), stream, 1, stream,
        "professional_duty_recording");
    static OperationProvenance Operation(EventStreamAddress stream, ReservationKey key) => new(Uuid.CreateVersion4(),
        key.RootOperationId, null, "Source use history fixture", ActorReference.ForPlatformOperator(Uuid.CreateVersion4(),
            "Operator"), stream, "initial", 0, new string('a', 64), NativeFacts.Now, stream, stream, key.FirstOwner);
    static FirmStaffProfessionalDutyUseAuthorized Granted(EventStreamAddress stream, ReservationKey key) => new(stream,
        1, (long)key.SourceGeneration, NativeFacts.Ref(SourceUseAuthorizationIntent.Schema), null, Operation(stream, key), null, null);
    static FirmStaffProfessionalDutyUseSettled Settled(EventStreamAddress stream, ReservationKey key) => new(stream,
        1, (long)key.SourceGeneration, NativeFacts.Ref(SourceUseAuthorizationIntent.Schema), null, Operation(stream, key), null, null);
    static DomainEvent[] Attach(EventStreamAddress stream, params DomainEvent[] facts) => facts.Select((fact, index) =>
        DomainEventSeed.Attach(fact, Uuid.Parse(stream.Resource, System.Globalization.CultureInfo.InvariantCulture),
            (ulong)index + 1)).ToArray();
    static SourceUseAggregateReader Reader(EventStreamAddress stream, DomainEvent[] facts) =>
        new(new ReplayReader(facts), new Records(stream, facts));

    sealed class ReservationOwner : Aggregate, ISourceUseOwner
    {
        public ReservationOwner(EventStreamAddress stream) : base(Uuid.Parse(stream.Resource,
            System.Globalization.CultureInfo.InvariantCulture), stream)
        {
            Uses = new SourceUseReservations(stream);
            On<FirmStaffProfessionalDutyUseAuthorized>(fact => Uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording, fact));
            On<FirmStaffProfessionalDutyUseSettled>(fact => Uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording, fact));
        }
        public SourceUseReservations Uses { get; }
        public NativeAcceptance Acceptance { get; } = new(Uuid.Empty);
        public Result Grant(ReservationKey key)
        {
            var operation = Operation(Stream, key);
            Uses.RequireForAppend(SourceUsePurpose.ProfessionalDutyRecording, operation, (long)key.SourceGeneration,
                Version, CommittedStreamPosition);
            RaiseEvent(Granted(Stream, key));
            return Result.Success;
        }
        protected override void Apply(DomainEvent value)
        {
            base.Apply(value);
            Uses.ObserveOwnerFact(value);
        }
    }

    sealed class ReplayReader(DomainEvent[] facts) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new AggregateScenario<TAggregate>(aggregate).Given(facts[(int)aggregate.Version..]).Aggregate);
        }
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public int Reads { get; private set; }
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            Reads++;
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
