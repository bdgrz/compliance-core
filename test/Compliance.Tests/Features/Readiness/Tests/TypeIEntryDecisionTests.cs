using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class TypeIEntryDecisionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Assessment = Uuid.CreateVersion4();
    static readonly Uuid Member = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static ActorReference Actor => ActorReference.ForMember(Member, "Org administrator");

    [Theory]
    [InlineData("enter_type_i")]
    [InlineData("not_yet")]
    public void ShouldCopyEveryGapAndPlanGivenCompleteHistoricalAssessment(string outcome)
    {
        // Arrange
        var command = Command(outcome);
        var owner = new TypeIEntryDecision(Tenant, command.DecisionId);
        var gap = Gap();
        var previous = Uuid.CreateVersion4();

        // Act
        var result = owner.Decide(command, Snapshot("not_ready", [gap]), previous, Actor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<TypeIEntryDecided>(Assert.Single(new AggregateScenario<TypeIEntryDecision>(owner).PendingEvents));
        Assert.Equal(Assessment, fact.AssessmentId);
        Assert.Equal(previous, fact.ReplacesDecisionId);
        Assert.Equal(gap, Assert.Single(fact.OpenGaps));
        Assert.Equal(outcome, fact.Outcome);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenIncompleteAssessment()
    {
        // Arrange
        var command = Command("enter_type_i");
        var owner = new TypeIEntryDecision(Tenant, command.DecisionId);

        // Act
        var result = owner.Decide(command, Snapshot("incomplete", [Gap()]), null, Actor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<TypeIEntryDecision>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldReplayOriginalAndRefuseChangedIntentGivenRepeatedDecisionIdentity()
    {
        // Arrange
        var command = Command("enter_type_i");
        var owner = new TypeIEntryDecision(Tenant, command.DecisionId);
        var snapshot = Snapshot("ready", []);
        Assert.True(owner.Decide(command, snapshot, null, Actor, Now).IsSuccess);

        // Act
        var retry = owner.Decide(command, snapshot, Uuid.CreateVersion4(), Actor, Now.AddDays(1));
        var changed = owner.Decide(command with { Outcome = "not_yet" }, snapshot, null, Actor, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(Now, owner.Recorded!.DecidedAt);
        Assert.Single(new AggregateScenario<TypeIEntryDecision>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAllowAnotherDecisionOnSameAssessmentGivenNewDecisionIdentity()
    {
        // Arrange
        var first = Command("enter_type_i");
        var second = Command("not_yet");
        var prior = new TypeIEntryDecision(Tenant, first.DecisionId);
        var next = new TypeIEntryDecision(Tenant, second.DecisionId);
        Assert.True(prior.Decide(first, Snapshot("ready", []), null, Actor, Now).IsSuccess);

        // Act
        var result = next.Decide(second, Snapshot("ready", []), first.DecisionId, Actor, Now.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(first.DecisionId, next.Recorded!.ReplacesDecisionId);
        Assert.Equal("enter_type_i", prior.Recorded!.Outcome);
        Assert.NotEqual(prior.Stream, next.Stream);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("approve_with_exceptions")]
    [InlineData("defer")]
    [InlineData("")]
    public void ShouldRefuseWithoutFactGivenLegacyOrMissingOutcome(string outcome)
    {
        // Arrange
        var command = Command(outcome);
        var owner = new TypeIEntryDecision(Tenant, command.DecisionId);

        // Act
        var result = owner.Decide(command, Snapshot("ready", []), null, Actor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<TypeIEntryDecision>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveUnplannedGapGivenManagementAcceptsKnownGap()
    {
        // Arrange
        var command = Command("enter_type_i");
        var owner = new TypeIEntryDecision(Tenant, command.DecisionId);
        var gap = Gap() with { Plan = null };

        // Act
        var result = owner.Decide(command, Snapshot("not_ready", [gap]), null, Actor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(Assert.Single(owner.Recorded!.OpenGaps).Plan);
    }

    static DecideTypeIEntryV3 Command(string outcome) => new(Uuid.CreateVersion4(), Tenant, Program, Assessment, outcome, "Management rationale");
    static TypeIEntryAssessmentSnapshot Snapshot(string verdict, IReadOnlyList<ReadinessGapView> gaps) => new(Tenant, Program, Assessment,
        "readiness-rules/historical", Now.AddMonths(-1), new string('a', 64), verdict, gaps);
    static ReadinessGapView Gap()
    {
        var gap = Uuid.CreateVersion4();
        return new ReadinessGapView(gap, "unmet", "subject", "rule", "Gap explanation", [],
            new ReadinessGapPlanView(gap, Member, new DateOnly(2026, 12, 1), "Resolve gap", Actor, Now) { Revision = 2 });
    }
}
