using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.FirmServices.Tests;

public sealed class IndependenceRuleSetTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldFreezeTermsGivenDraftThenRatification()
    {
        // Arrange
        var source = IndependenceRuleSetTerms.Initial.Classifications.ToList();
        var terms = new IndependenceRuleSetTerms(12, source);
        var owner = new IndependenceRuleSet(1);
        Assert.True(owner.Draft(new(Uuid.CreateVersion4(), 1, terms), null, UserId, Now).IsSuccess);
        source.Clear();

        // Act
        var ratified = owner.Ratify(new(Uuid.CreateVersion4(), 1, 1), UserId, Now.AddDays(1));
        var revised = owner.Revise(new(Uuid.CreateVersion4(), 1, 2, IndependenceRuleSetTerms.Initial with
            { LookBackMonths = 24 }), UserId, Now.AddDays(2));

        // Assert
        Assert.True(ratified.IsSuccess);
        Assert.False(revised.IsSuccess);
        Assert.True(owner.View!.IsRatified);
        Assert.Equal(5, owner.View.Terms.Classifications.Count);
        Assert.Equal(12, owner.View.Terms.LookBackMonths);
        Assert.Equal(2, new AggregateScenario<IndependenceRuleSet>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenSkippedPredecessorVersion()
    {
        // Arrange
        var owner = new IndependenceRuleSet(3);
        var predecessor = new IndependenceRuleSetView(1, 2, IndependenceRuleSetTerms.Initial, true, UserId, Now);

        // Act
        var result = owner.Draft(new(Uuid.CreateVersion4(), 3, IndependenceRuleSetTerms.Initial), predecessor,
            UserId, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(owner.View);
        Assert.Empty(new AggregateScenario<IndependenceRuleSet>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenUnratifiedPredecessor()
    {
        // Arrange
        var owner = new IndependenceRuleSet(2);
        var predecessor = new IndependenceRuleSetView(1, 1, IndependenceRuleSetTerms.Initial, false, UserId, Now);

        // Act
        var result = owner.Draft(new(Uuid.CreateVersion4(), 2, IndependenceRuleSetTerms.Initial), predecessor,
            UserId, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<IndependenceRuleSet>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldReplayOriginalDraftGivenLaterRevisionAndRatification()
    {
        // Arrange
        var request = new DraftIndependenceRuleSet(Uuid.CreateVersion4(), 1, IndependenceRuleSetTerms.Initial);
        var source = new IndependenceRuleSet(1);
        Assert.True(source.Draft(request, null, UserId, Now).IsSuccess);
        Assert.True(source.Revise(new(Uuid.CreateVersion4(), 1, 1, IndependenceRuleSetTerms.Initial with
            { LookBackMonths = 24 }), UserId, Now.AddDays(1)).IsSuccess);
        Assert.True(source.Ratify(new(Uuid.CreateVersion4(), 1, 2), UserId, Now.AddDays(2)).IsSuccess);
        var owner = NativeFacts.Replay(new IndependenceRuleSet(1, request.CommandId), NativeFacts.Raised(source));

        // Act
        var result = owner.Draft(request, null, UserId, Now.AddDays(3));
        var changed = owner.Draft(request with { Terms = request.Terms with { LookBackMonths = 36 } },
            null, UserId, Now.AddDays(3));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.Terms.LookBackMonths);
        Assert.False(result.Value.IsRatified);
        Assert.False(changed.IsSuccess);
        Assert.True(owner.View!.IsRatified);
        Assert.Equal(24, owner.View.Terms.LookBackMonths);
        Assert.Empty(new AggregateScenario<IndependenceRuleSet>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldLeaveRevisionUnchangedGivenEquivalentDraftTerms()
    {
        // Arrange
        var owner = new IndependenceRuleSet(1);
        Assert.True(owner.Draft(new(Uuid.CreateVersion4(), 1, IndependenceRuleSetTerms.Initial), null, UserId, Now).IsSuccess);

        // Act
        var result = owner.Revise(new(Uuid.CreateVersion4(), 1, 1, IndependenceRuleSetTerms.Initial), UserId, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, owner.Revision);
        Assert.Single(new AggregateScenario<IndependenceRuleSet>(owner).PendingEvents);
    }
}
