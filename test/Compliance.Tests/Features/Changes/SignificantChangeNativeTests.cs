using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Changes;

public sealed class SignificantChangeNativeTests
{
    [Fact]
    public async Task ShouldReadHistoryGivenPreparationTimestampRegressesAcrossValidNativeAppends()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var rules = new SignificantChangeRules(fixture.Tenant, fixture.Program, fixture.Change);
        var work = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var started = fixture.Clock.Now;
        Assert.Null(rules.Record(fixture.Body(), member, "Recorder", started));
        Assert.Null(rules.Assess(1, new SignificantChangeImpactContent("significant", "Reviewed material system boundary and description impact.",
            true, false, false, false, ["scope", "system_description"], [new("control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)],
            [work], [], [], [], true), assessor, "Assessor", started.AddMinutes(1)));
        Assert.Null(rules.LinkReviewWork(2, [work], member, "Work linker", started.AddMinutes(3)));
        Assert.Null(rules.RecordSystemDescriptionDecision(3, "no_change", Uuid.CreateVersion4(), "Exact independent decision.",
            member, "Decision maker", started.AddMinutes(2)));
        fixture.Clock.Now = started.AddMinutes(4);
        await ChangeSeeds.WriteAsync(fixture.Writer, fixture.Content, rules);

        // Act
        var current = await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.Equal(4, current.View!.Revision);
        Assert.True(current.View.PeriodCloseEligible);
        var facts = await fixture.FactsAsync();
        Assert.True(((ISignificantChangeFact)facts[3]).Operation.AcceptedAt < ((ISignificantChangeFact)facts[2]).Operation.AcceptedAt);
    }

    [Fact]
    public async Task ShouldExcludePreparedBeforeCutGivenNativeAppendCommitsAfterRecordedThrough()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var original = fixture.Body();
        await fixture.RecordAsync(original);
        var cutoff = fixture.Clock.Now.AddMinutes(1);
        fixture.Content.BeforeWrite = () => fixture.Clock.Now = cutoff.AddMinutes(1);
        var request = new ReviseSignificantChange(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1,
            original with { Title = "Prepared before cut but committed later" }, "Later append");
        Assert.True((await new ReviseSignificantChangeHandler(fixture.Operations).HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);

        // Act
        var historical = await fixture.Changes.ReadThroughAsync(fixture.Tenant, fixture.Program, fixture.Change, cutoff);
        var current = await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.Equal(original.Title, historical.View!.Current!.Content.Title);
        Assert.Equal(1, historical.View.Revision);
        Assert.Equal("Prepared before cut but committed later", current.View!.Current!.Content.Title);
        Assert.True(((ISignificantChangeFact)(await fixture.FactsAsync())[^1]).Operation.AcceptedAt < cutoff);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcceptanceGivenLaterRevisionAndLostResponseRetry()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        var request = new RecordSignificantChange(fixture.Tenant, fixture.Program, fixture.Change, body);
        var handler = new RecordSignificantChangeHandler(fixture.Operations);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        var revised = await new ReviseSignificantChangeHandler(fixture.Operations).HandleAsync(fixture.Context(
            new ReviseSignificantChange(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1, body with { Title = "Corrected migration" }, "Correct the title")), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);

        // Act
        var replay = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var changedIntent = await handler.HandleAsync(fixture.Context(request with { Content = body with { Title = "Different authored creation" } }), CancellationToken.None);
        var changedActor = await handler.HandleAsync(fixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var current = await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.False(changedIntent.IsSuccess);
        Assert.False(changedActor.IsSuccess);
        var facts = await fixture.FactsAsync();
        Assert.Equal(2, facts.Count);
        Assert.All(facts, value => Assert.Equal(current.Owner.Stream, ((ISignificantChangeFact)value).Operation.FirstOwner));
        Assert.Equal("Corrected migration", current.View!.Current!.Content.Title);
        Assert.Equal(2, current.Owner.NativeSequence);
    }

    [Fact]
    public async Task ShouldPersistOriginalUnitGivenFreshCommandRepeatsSameCorrectionAtCurrentRevision()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        await fixture.RecordAsync(body);
        var handler = new ReviseSignificantChangeHandler(fixture.Operations);
        var first = new ReviseSignificantChange(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1,
            body with { Title = "Exact correction" }, "Correct the title");
        Assert.True((await handler.HandleAsync(fixture.Context(first), CancellationToken.None)).IsSuccess);
        var next = first with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 };

        // Act
        var accepted = await handler.HandleAsync(fixture.Context(next), CancellationToken.None);
        var replay = await handler.HandleAsync(fixture.Context(next), CancellationToken.None);
        var current = await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(2, current.View!.Revision);
        Assert.Equal(3, current.Owner.NativeSequence);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldRecoverEqualWinnerGivenImmutablePreparationFailsAfterConcurrentAcceptance()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        await fixture.RecordAsync(body);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        var handler = new ReviseSignificantChangeHandler(fixture.Operations);
        var request = new ReviseSignificantChange(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1,
            body with { Title = "A concurrent exact correction" }, "Correct the title");
        fixture.Content.BeforeRead = async () =>
        {
            var winner = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        };

        // Act
        var recovered = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(2, (await fixture.FactsAsync()).Count);
        Assert.Equal(2, (await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change)).View!.Revision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyExactImmutableBodyFailureGivenCurrentScopedRead(bool unavailable)
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        await fixture.RecordAsync();
        var before = (await fixture.FactsAsync()).Count;
        fixture.Content.Failure = reference => reference.Schema == SignificantChangeContentStore.BodySchema
            ? (unavailable ? new ImmutableContentUnavailableException("The body is temporarily absent.") : new ImmutableContentIntegrityException("The body hash differs.")) : null;
        var handler = new GetSignificantChangeHandler(fixture.Changes, new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)));

        // Act
        var result = await handler.HandleAsync(fixture.Context(new GetSignificantChange(fixture.Tenant, fixture.Program, fixture.Change)), CancellationToken.None);
        var foreign = await handler.HandleAsync(fixture.Context(new GetSignificantChange(fixture.Tenant, Uuid.CreateVersion4(), fixture.Change)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.Equal(before, (await fixture.FactsAsync()).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldLeaveOwnerUnchangedGivenUnreadableExactPreparationContent(bool unavailable)
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        await fixture.RecordAsync(body);
        fixture.Content.Failure = reference => reference.Schema == SignificantChangeContentStore.BodySchema
            ? (unavailable ? new ImmutableContentUnavailableException("Temporarily absent") : new ImmutableContentIntegrityException("Digest differs")) : null;
        var request = new ReviseSignificantChange(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1,
            body with { Title = "Cannot accept unreadable prior state" }, "Correct title");

        // Act
        var result = await new ReviseSignificantChangeHandler(fixture.Operations).HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Single(await fixture.FactsAsync());
    }

    [Fact]
    public async Task ShouldUseReceiptCutGivenLaterContentCorrection()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        await fixture.RecordAsync(body);
        var cutoff = fixture.Clock.Now;
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        Assert.True((await new ReviseSignificantChangeHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSignificantChange(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Change, 1, body with { Title = "After cutoff" }, "Later source correction")), CancellationToken.None)).IsSuccess);

        // Act
        var historic = await fixture.Changes.ReadThroughAsync(fixture.Tenant, fixture.Program, fixture.Change, cutoff);
        var current = await fixture.Changes.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.Equal(body.Title, historic.View!.Current!.Content.Title);
        Assert.Equal(1, historic.View.Revision);
        Assert.Equal("After cutoff", current.View!.Current!.Content.Title);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholePageGivenListedNativeChangeBodyUnavailableOrCorrupt(bool unavailable)
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        await fixture.RecordAsync();
        var directory = new FitzSignificantChangeDirectory(new InMemoryKvClient(), fixture.Changes);
        await ProjectAsync(directory, fixture);
        fixture.Content.Failure = reference => reference.Schema == SignificantChangeContentStore.BodySchema
            ? (unavailable ? new ImmutableContentUnavailableException("Missing native body") : new ImmutableContentIntegrityException("Corrupt native body")) : null;
        var handler = new ListSignificantChangesHandler(directory, new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
            new SignificantChangeDirectoryReadConsistency(directory, fixture.Events));

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ListSignificantChanges(fixture.Tenant, fixture.Program, 50)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Single(await fixture.FactsAsync());
    }

    [Fact]
    public async Task ShouldFenceDirectoryLagAndPreserveCurrentSummaryGivenScopedList()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        await fixture.RecordAsync();
        var directory = new FitzSignificantChangeDirectory(new InMemoryKvClient(), fixture.Changes);
        var consistency = new SignificantChangeDirectoryReadConsistency(directory, fixture.Events);
        var handler = new ListSignificantChangesHandler(directory, new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)), consistency);
        var context = fixture.Context(new ListSignificantChanges(fixture.Tenant, fixture.Program, 50, null));

        // Act
        var behind = await handler.HandleAsync(context, CancellationToken.None);
        await ProjectAsync(directory, fixture);
        var current = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(behind.IsSuccess);
        Assert.True(behind.Error!.IsTransient);
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal("Production migration", Assert.Single(current.Value.Items).Name);
    }
    [Fact]
    public async Task ShouldRefuseNonPrefixReceiptCutGivenBrokerTimestampsDoNotOrderCausally()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        var body = fixture.Body();
        await fixture.RecordAsync(body);
        var cutoff = fixture.Clock.Now;
        Assert.True((await new ReviseSignificantChangeHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSignificantChange(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Change, 1, body with { Title = "Later causal event" }, "Correction")), CancellationToken.None)).IsSuccess);
        var receipts = new ChosenReceipts(source => ((ISignificantChangeFact)source.Event).NativeSequence == 1 ? cutoff.AddMinutes(1) : cutoff.AddMinutes(-1));
        var reader = new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Content, receipts);

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.ReadThroughAsync(fixture.Tenant, fixture.Program, fixture.Change, cutoff));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(2, receipts.Reads);
    }
    [Fact]
    public async Task ShouldNotReadReceiptAuthorityGivenOrdinaryCurrentHistoryQuery()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        await fixture.RecordAsync();
        var receipts = new ChosenReceipts(_ => throw new InvalidOperationException("Current reads never require receipt network calls."));
        var reader = new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Content, receipts);

        // Act
        var current = await reader.ReadAsync(fixture.Tenant, fixture.Program, fixture.Change);

        // Assert
        Assert.NotNull(current.View);
        Assert.Equal(0, receipts.Reads);
    }
    [Fact]
    public async Task ShouldRemainPendingGivenRecordedThroughReaderHasNoReceiptAuthority()
    {
        // Arrange
        await using var fixture = new SignificantChangeNativeFixture();
        await fixture.RecordAsync();
        var reader = new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Content);

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.ReadThroughAsync(fixture.Tenant, fixture.Program, fixture.Change, fixture.Clock.Now));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }
    sealed class ChosenReceipts(Func<DomainEventRecord, DateTimeOffset> at) : INativeEventReceiptReader
    {
        public int Reads { get; private set; }
        public ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default)
        { Reads++; return ValueTask.FromResult(at(source)); }
    }

    static async Task ProjectAsync(FitzSignificantChangeDirectory directory, SignificantChangeNativeFixture fixture)
    {
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);
        var pattern = EventStreamPattern.ForPattern(fixture.Tenant.ToString(), SignificantChange.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(SignificantChangeDirectoryProjector.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in fixture.Events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
