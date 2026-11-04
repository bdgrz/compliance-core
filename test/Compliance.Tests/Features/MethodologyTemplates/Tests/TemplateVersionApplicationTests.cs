using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class TemplateVersionApplicationTests
{
    [Fact]
    public void ShouldCompleteOnlyAfterEveryEntryHasOneOutcomeGivenMixedOwnerAnswers()
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), ["control", "policy", "risk"]).IsSuccess);

        // Act
        var created = scenario.Aggregate.Record(new TemplateEntryOutcome("control", "created", Uuid.CreateVersion4(), null));
        var completedEarly = scenario.Aggregate.IsCompleted;
        var proposed = scenario.Aggregate.Record(new TemplateEntryOutcome("policy", "proposed", Uuid.CreateVersion4(), null));
        var completedBeforeLast = scenario.Aggregate.IsCompleted;
        var refused = scenario.Aggregate.Record(new TemplateEntryOutcome("risk", "refused", null, "identifier_in_use"));

        // Assert
        Assert.True(created.IsSuccess);
        Assert.True(proposed.IsSuccess);
        Assert.True(refused.IsSuccess);
        Assert.False(completedEarly);
        Assert.False(completedBeforeLast);
        Assert.True(scenario.Aggregate.IsCompleted);
        Assert.Equal(new TemplateApplicationOutcomeCounts(1, 1, 1), scenario.Aggregate.Counts);
        Assert.Equal(5, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldKeepExactRetryFactFreeAndRefuseChangedIntentGivenCompletedEntry()
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), ["control"]).IsSuccess);
        var outcome = new TemplateEntryOutcome("control", "created", Uuid.CreateVersion4(), null);
        Assert.True(scenario.Aggregate.Record(outcome).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var retry = scenario.Aggregate.Record(outcome with { });
        var changed = scenario.Aggregate.Record(outcome with { RecordId = Uuid.CreateVersion4() });

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(new TemplateApplicationOutcomeCounts(1, 0, 0), scenario.Aggregate.Counts);
    }

    [Fact]
    public void ShouldKeepOpeningRetryFactFreeAndRejectAnotherSourceGivenOpenedVersion()
    {
        // Arrange
        var scenario = Scenario();
        var source = Uuid.CreateVersion4();
        Assert.True(scenario.Aggregate.Open(source, ["control", "risk"]).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var retry = scenario.Aggregate.Open(source, ["risk", "control"]);
        var changedSource = scenario.Aggregate.Open(Uuid.CreateVersion4(), ["control", "risk"]);
        var changedEntries = scenario.Aggregate.Open(source, ["control"]);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changedSource.IsSuccess);
        Assert.False(changedEntries.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
    }

    [Theory]
    [InlineData("unknown", "created", false, null)]
    [InlineData("control", "created", true, null)]
    [InlineData("control", "proposed", true, null)]
    [InlineData("control", "refused", true, null)]
    [InlineData("control", "refused", false, "identifier_in_use")]
    [InlineData("control", "other", false, null)]
    public void ShouldRefuseWithoutFactGivenInvalidOwnerOutcome(string entryKey, string outcome, bool noRecord, string? reason)
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), ["control"]).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.Record(new TemplateEntryOutcome(entryKey, outcome,
            noRecord ? null : Uuid.CreateVersion4(), reason));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.False(scenario.Aggregate.IsCompleted);
    }

    [Fact]
    public void ShouldKeepOutcomeRetryableGivenOpeningHasNotArrived()
    {
        // Arrange
        var scenario = Scenario();

        // Act
        var result = scenario.Aggregate.Record(new TemplateEntryOutcome("control", "created", Uuid.CreateVersion4(), null));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error.IsTransient);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReplayCompletionAndRefuseChangedOutcomeGivenRetainedFacts()
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), ["risk"]).IsSuccess);
        var refused = new TemplateEntryOutcome("risk", "refused", null, "identifier_in_use");
        Assert.True(scenario.Aggregate.Record(refused).IsSuccess);
        var replay = new AggregateScenario<TemplateVersionApplication>(new TemplateVersionApplication(
            Uuid.Parse(scenario.Aggregate.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture),
            ProgramId, TemplateId, 1)).Given(scenario.PendingEvents.ToArray());

        // Act
        var retry = replay.Aggregate.Record(refused);
        var changed = replay.Aggregate.Record(refused with { RefusalReason = "other_reason" });

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.True(replay.Aggregate.IsCompleted);
        Assert.Equal(new TemplateApplicationOutcomeCounts(0, 0, 1), replay.Aggregate.Counts);
        Assert.Empty(replay.PendingEvents);
    }

    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid TemplateId = Uuid.CreateVersion4();

    static AggregateScenario<TemplateVersionApplication> Scenario() => new(new TemplateVersionApplication(
        Uuid.CreateVersion4(), ProgramId, TemplateId, 1));
}
