using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ControlOccurrenceFanOutTests
{
    [Fact]
    public async Task ShouldMaterializeEverySelectedOccurrenceOnceGivenFanOut()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);

        // Act
        var run = await fixture.Host.RunFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var rerun = await fixture.Host.RunFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var opened = await OpenedAsync(fixture, selected);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal(ProcessState.Completed, rerun.State);
        Assert.Equal(4, opened.Count);
        Assert.All(opened, fact =>
        {
            Assert.Equal(ControlOperationStates.Expected, fact.Kind);
            Assert.Equal(1, fact.Revision);
            Assert.Equal(selected.Operation.RootOperationId, fact.Operation.RootOperationId);
            Assert.Equal(selected.Metadata!.EventId, fact.Operation.CausationEventId);
            Assert.Null(fact.FirstResponse);
        });
        var coverage = await CoverageAsync(fixture, period);
        Assert.True(coverage.IsComplete);
        Assert.Equal(4, coverage.Materialized.Count);
    }

    [Fact]
    public async Task ShouldStartFanOutFromCommittedSelectionGivenRootReactor()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);

        // Act
        await fixture.Host.ReactFanOutAsync(fixture.StreamOf(period));
        await fixture.Host.ReactFanOutAsync(fixture.StreamOf(period));
        var record = await fixture.Host.Records.ReadAsync(TypeIIRangeHost.FanOutKeyOf(fixture.TenantId,
            selected.Operation.RootOperationId));
        var opened = await OpenedAsync(fixture, selected);

        // Assert
        Assert.Equal(ProcessState.Completed, record!.State);
        Assert.Equal(4, record.Targets.Count);
        Assert.Equal(selected.CreationIntentManifest, record.Intent);
        Assert.Equal(4, opened.Count);
    }

    [Fact]
    public async Task ShouldResumeWithoutDuplicateOccurrencesGivenCrashAfterSomeReceivers()
    {
        // Arrange
        var crash = new CrashAfterReplaces();
        await using var fixture = new TypeIIRangeFixture(crash.Wrap);
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);
        var record = await fixture.Host.StartFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        crash.ArmAfterReplaceCount(4);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.Host.RunFanOutAsync(fixture.TenantId,
            fixture.ProgramId, period, selected));
        var partial = await OpenedAsync(fixture, selected);
        var interrupted = await fixture.Host.Records.ReadAsync(record.Key);
        crash.Disarm();
        var resumed = await fixture.Host.RunFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var opened = await OpenedAsync(fixture, selected);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.InRange(partial.Count, 1, 3);
        Assert.NotEqual(ProcessState.Completed, interrupted!.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(4, opened.Count);
        Assert.Equal(opened.Select(fact => fact.OccurrenceId).Distinct().Count(), opened.Count);
        foreach (var fact in partial)
            Assert.Contains(opened, final => final.Metadata!.EventId == fact.Metadata!.EventId);
    }

    [Fact]
    public async Task ShouldReturnOriginalFactGivenSecondGeneratorSelectingSameWindow()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);
        var record = await fixture.Host.StartFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var target = record.Targets[0];
        var first = await fixture.Host.FanOutStepAsync(record, target);
        var applied = await fixture.Host.SendAsync(first);
        var second = first with { CommandId = Uuid.CreateVersion4() };

        // Act
        var other = await fixture.Host.SendAsync(second);
        var replay = await fixture.Host.SendAsync(first);
        var opened = await OpenedAsync(fixture, selected);

        // Assert
        var original = Assert.IsType<ReceiverOutcome.Applied>(applied.Value).Fact;
        Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(other.Value).Fact);
        Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Single(opened);
        Assert.Equal(Uuid.Parse(target, System.Globalization.CultureInfo.InvariantCulture), opened[0].OccurrenceId);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedFrozenTermsForSameWindow()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);
        var record = await fixture.Host.StartFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var command = await fixture.Host.FanOutStepAsync(record, record.Targets[0]);
        var frozen = await fixture.Host.Content.ReadJsonAsync<ControlOccurrenceCreationIntent>(
            fixture.TenantId.ToString(), command.Intent);
        var changed = frozen with { Owner = new OperatingHolder("member", Uuid.CreateVersion4()) };
        var changedRef = await fixture.Host.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(),
            ControlOccurrenceCreationIntent.Schema, changed);

        // Act
        var forged = await fixture.Host.SendAsync(command with { Intent = changedRef });
        var opened = await OpenedAsync(fixture, selected);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, forged.Error?.Kind);
        Assert.False(forged.Error!.IsTransient);
        Assert.Empty(opened);
        Assert.False(SameTerms(frozen, changed));
    }

    [Fact]
    public async Task ShouldNotMaterializeGivenSourceThatIsNotTheSelection()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var selected = await SelectAllAsync(fixture, period);
        var record = await fixture.Host.StartFanOutAsync(fixture.TenantId, fixture.ProgramId, period, selected);
        var command = await fixture.Host.FanOutStepAsync(record, record.Targets[0]);
        var missing = new NativeFactReference(command.Source.Stream, Uuid.CreateVersion4(), 2);

        // Act
        var unseen = await fixture.Host.SendAsync(command with { Source = missing });
        var wrongPeriod = await fixture.Host.SendAsync(command with
        {
            Source = command.Source with { AggregateVersion = command.Source.AggregateVersion + 1 },
        });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, unseen.Error?.Kind);
        Assert.True(unseen.Error!.IsTransient);
        Assert.False(wrongPeriod.IsSuccess);
        Assert.Empty(await OpenedAsync(fixture, selected));
    }

    [Fact]
    public async Task ShouldRetainCompleteManifestGivenMoreSelectionsThanOldEventCap()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var content = fixture.Host.Content;
        var realm = fixture.TenantId.ToString();
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ControlOccurrenceCreationIntent.Schema,
            new ControlOccurrenceCreationIntent(new DurableIntentHeader(Uuid.CreateVersion4(),
                    new EventStreamAddress(realm, "a", "b"), new EventStreamAddress(realm, "a", "b"),
                    new EventStreamAddress(realm, "a", "b"),
                    Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(Uuid.CreateVersion4(), "x"),
                    "initial", null, new string('a', 64), DateTimeOffset.UtcNow), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, Uuid.CreateVersion4(), new ControlCalendarWindow(fixture.FirstStart, fixture.FirstEnd),
                Uuid.CreateVersion4(), new NativeFactReference(new EventStreamAddress(realm, "a", "b"),
                    Uuid.CreateVersion4(), 1), new OperatingHolder("member", fixture.OwnerMemberId), null,
                fixture.ReviewerMemberId, "recurring_schedule", DateTimeOffset.UtcNow));
        var selections = Enumerable.Range(0, 5000).Select(_ => new ControlOccurrenceSelection(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), intent)).ToArray();

        // Act
        var reference = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            ControlOccurrenceSelectionManifest.Schema, new ControlOccurrenceSelectionManifest(selections));
        var read = await content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(realm, reference);

        // Assert
        Assert.Equal(5000, read.Selections.Count);
        Assert.Equal(selections.Select(selection => selection.OccurrenceId),
            read.Selections.Select(selection => selection.OccurrenceId));
    }

    [Fact]
    public async Task ShouldDefineUnorderedPerTargetMaterializationGivenFanOutDefinition()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();

        // Act
        var definition = fixture.Host.FanOutDefinition;

        // Assert
        Assert.False(definition.Ordered);
        Assert.Equal([nameof(DueControlOccurrencesGenerated)], definition.RootSourceEvents);
        var stage = Assert.Single(definition.Stages);
        Assert.Equal("materialize", stage.Stage);
        Assert.True(stage.IsPerTarget);
        Assert.Equal([nameof(MaterializeControlOccurrence)], stage.Receivers);
        Assert.Empty(definition.Abort);
    }

    [Fact]
    public void ShouldDeriveOneIdentityPerControlAndBusinessWindowGivenAnyGeneratorOrTrigger()
    {
        // Arrange
        var (tenant, program, control) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var window = new ControlCalendarWindow(new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1));

        // Act
        var first = ControlOccurrenceIdentity.Of(tenant, program, control, window);
        var again = ControlOccurrenceIdentity.Of(tenant, program, control, window with { });
        var shifted = ControlOccurrenceIdentity.Of(tenant, program, control,
            new ControlCalendarWindow(window.From, window.UntilExclusive.AddDays(1)));
        var otherControl = ControlOccurrenceIdentity.Of(tenant, program, Uuid.CreateVersion4(), window);
        var eventWindow = ControlOccurrenceIdentity.Of(tenant, program, control, new ControlEventWindow("release", "4.2"));

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, shifted);
        Assert.NotEqual(first, otherControl);
        Assert.NotEqual(first, eventWindow);
        Assert.False(new ControlCalendarWindow(window.From, window.From).IsValid);
        Assert.False(new ControlEventWindow(" release", "4.2").IsValid);
    }

    static async Task<(Uuid Period, ControlOperatingPlanView Plan)> StartAsync(TypeIIRangeFixture fixture)
    {
        var period = await fixture.StartPeriodAsync();
        return (period, await fixture.SeedOperatingPlanAsync());
    }

    static GenerateDueControlOccurrences Request(TypeIIRangeFixture fixture, Uuid period, string key) =>
        new(fixture.TenantId, fixture.ProgramId, period, fixture.FirstEnd, key);

    static ValueTask<Result> Generate(TypeIIRangeFixture fixture, GenerateDueControlOccurrences request) =>
        fixture.Host.GenerateHandler(fixture.Clock).HandleAsync(fixture.Context(request), CancellationToken.None);

    static async Task<DueControlOccurrencesGenerated> SelectAllAsync(TypeIIRangeFixture fixture, Uuid period)
    {
        var generated = await Generate(fixture, Request(fixture, period, "generation-001"));
        Assert.True(generated.IsSuccess, generated.Error?.Message);
        return Assert.Single(await fixture.Host.EventsAsync<DueControlOccurrencesGenerated>(fixture.StreamOf(period)));
    }

    static async Task<List<ControlOccurrenceOpened>> OpenedAsync(TypeIIRangeFixture fixture,
        DueControlOccurrencesGenerated selected)
    {
        var manifest = await fixture.Host.Content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(
            fixture.TenantId.ToString(), selected.CreationIntentManifest);
        var opened = new List<ControlOccurrenceOpened>();
        foreach (var selection in manifest.Selections)
            opened.AddRange(await fixture.Host.EventsAsync<ControlOccurrenceOpened>(new ControlOccurrence(
                fixture.TenantId, fixture.ProgramId, fixture.ControlId, selection.OccurrenceId).Stream));
        return opened;
    }

    static async Task<TypeIIOccurrenceCoverageProof> CoverageAsync(TypeIIRangeFixture fixture, Uuid period)
    {
        await using var scope = fixture.Host.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return await services.GetRequiredService<TypeIIOccurrenceCoverage>().ProveAsync(
            await fixture.PeriodAsync(period), CancellationToken.None);
    }

    static bool SameTerms(ControlOccurrenceCreationIntent left, ControlOccurrenceCreationIntent right) =>
        left.SameTermsAs(right);

    sealed class CrashAfterReplaces
    {
        int _remaining = int.MaxValue;

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public void Disarm() => _remaining = int.MaxValue;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashAfterReplaces owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion, CancellationToken ct = default)
            {
                if (owner._remaining-- == 0)
                    throw new InvalidOperationException("Worker crashed before the CAS.");
                return inner.ReplaceAsync(record, expectedVersion, ct);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore, int limit,
                CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }
}
