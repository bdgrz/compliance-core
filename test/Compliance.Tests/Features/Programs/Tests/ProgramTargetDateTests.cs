using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Versioning.Models;

namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramTargetDateTests
{
    static readonly DateOnly Earlier = new(2028, 3, 1);
    static readonly DateOnly Later = new(2028, 4, 1);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid Author = Uuid.CreateVersion4();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    public void ShouldRefuseCreationWithoutChangeGivenEarlierStageTargetAfterLaterStageTarget(
        int earlierStage, int laterStage)
    {
        // Arrange
        var program = NewProgram();
        var dates = new DateOnly?[4];
        dates[earlierStage] = Later;
        dates[laterStage] = Earlier;

        // Act
        var failure = program.Create("SOC 2", Plan(dates), Author, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.False(program.IsCreated);
        Assert.Equal(0, program.Revision);
        Assert.Empty(program.AppliedChanges);
        Assert.Empty(program.PendingChanges);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    public void ShouldKeepPreviousTargetsGivenRevisionReversesPresentTargetDates(
        int earlierStage, int laterStage)
    {
        // Arrange
        var program = NewProgram();
        var original = Plan([Earlier, null, Later, null]);
        Assert.Null(program.Create("SOC 2", original, Author, "Lead", Now));
        program.ClearPendingChanges();
        var before = program.AppliedChanges.ToArray();
        var dates = new DateOnly?[4];
        dates[earlierStage] = Later;
        dates[laterStage] = Earlier;

        // Act
        var failure = program.Revise(1, "SOC 2", Plan(dates), Author, "Lead", Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, program.Revision);
        Assert.Equal(before, program.AppliedChanges);
        Assert.Equal(original, Assert.Single(program.AppliedChanges).Content!.Plan);
        Assert.Empty(program.PendingChanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void ShouldPreserveUnknownDatesGivenOrderedPresentTargets(int presentDates)
    {
        // Arrange
        var program = NewProgram();
        DateOnly?[] dates = Enumerable.Range(0, 4)
            .Select(index => (presentDates & (1 << index)) == 0 ? (DateOnly?)null : Earlier.AddMonths(index))
            .ToArray();
        var plan = Plan(dates);

        // Act
        var created = program.Create("SOC 2", plan, Author, "Lead", Now);
        var revised = program.Revise(1, "SOC 2 revised", plan, Author, "Lead", Now.AddDays(1));

        // Assert
        Assert.Null(created);
        Assert.Null(revised);
        Assert.Equal(2, program.Revision);
        Assert.Equal(plan, program.AppliedChanges[0].Content!.Plan);
        Assert.Equal(plan, program.AppliedChanges[1].Content!.Plan);
        Assert.Equal("Lead", program.AppliedChanges[1].Actor.Display);
        Assert.Equal(Now.AddDays(1), program.AppliedChanges[1].ChangedAt);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    public void ShouldAllowEqualPresentDatesGivenTargetsShareAnAllowedDate(int presentDates)
    {
        // Arrange
        var program = NewProgram();
        DateOnly?[] dates = Enumerable.Range(0, 4)
            .Select(index => (presentDates & (1 << index)) == 0 ? (DateOnly?)null : Earlier)
            .ToArray();
        var plan = Plan(dates);

        // Act
        var created = program.Create("SOC 2", plan, Author, "Lead", Now);
        var revised = program.Revise(1, "SOC 2 revised", plan, Author, "Lead", Now.AddDays(1));

        // Assert
        Assert.Null(created);
        Assert.Null(revised);
        Assert.Equal(plan, program.AppliedChanges[0].Content!.Plan);
        Assert.Equal(plan, program.AppliedChanges[1].Content!.Plan);
    }

    static ProgramRules NewProgram() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static ProgramPlan Plan(DateOnly?[] dates) => new(dates[0], dates[1], dates[2], dates[3], null, null);
}
