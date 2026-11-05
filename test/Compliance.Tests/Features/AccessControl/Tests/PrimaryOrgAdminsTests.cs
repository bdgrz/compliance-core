using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PrimaryOrgAdminsTests
{
    [Fact]
    public void ShouldReserveTwoSeatsGivenPendingDesignation()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        // Act
        Assert.True(owner.Designate(first, Uuid.CreateVersion4(), episode).IsSuccess);
        Assert.True(owner.Designate(second, Uuid.CreateVersion4(), episode).IsSuccess);
        var third = owner.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), episode);
        // Assert
        Assert.Equal(2, owner.SeatCount);
        Assert.Equal(0, owner.DesignatedCount);
        Assert.False(third.IsSuccess);
    }

    [Fact]
    public void ShouldRefuseReleaseGivenOnlyPendingReplacement()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var user = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True(owner.Designate(designation, user, episode).IsSuccess);
        Assert.True(owner.RecordOutcome(designation, user, episode, null).IsSuccess);
        Assert.True(owner.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), episode).IsSuccess);
        var scenario = new AggregateScenario<PrimaryOrgAdmins>(owner);
        var before = scenario.PendingEvents.ToArray();
        // Act
        var result = owner.Release(Uuid.CreateVersion4(), user, designation, "Replacement");
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepOneDesignatedGivenSequentialCompetingReleases()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var users = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var ids = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var episode = Uuid.CreateVersion4();
        for (var i = 0; i < 2; i++)
        {
            Assert.True(owner.Designate(ids[i], users[i], episode).IsSuccess);
            Assert.True(owner.RecordOutcome(ids[i], users[i], episode, null).IsSuccess);
        }
        // Act
        var first = owner.Release(Uuid.CreateVersion4(), users[0], ids[0], "Replacement");
        var second = owner.Release(Uuid.CreateVersion4(), users[1], ids[1], "Replacement");
        // Assert
        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(1, owner.DesignatedCount);
    }

    [Fact]
    public void ShouldFreeSeatGivenRefusedMemberMark()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var user = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True(owner.Designate(id, user, episode).IsSuccess);
        // Act
        var result = owner.RecordOutcome(id, user, episode, "guest");
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, owner.SeatCount);
    }

    [Fact]
    public void ShouldRefuseChangedIntentGivenRepeatedDesignationIdentity()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4(), id);
        var user = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True(owner.Designate(id, user, episode).IsSuccess);
        // Act
        var exact = owner.Designate(id, user, episode);
        var changed = owner.Designate(id, Uuid.CreateVersion4(), episode);
        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(changed.IsSuccess);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseMembershipWithdrawalGivenPrimaryMark(bool deprovision)
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register().IsSuccess);
        Assert.True(member.MarkPrimary(Uuid.CreateVersion4(), member.MembershipEpisodeId).IsSuccess);
        var scenario = new AggregateScenario<Member>(member);
        var before = scenario.PendingEvents.ToArray();
        // Act
        var result = deprovision
            ? member.PrepareDeprovision(member.MembershipEpisodeId, -1, "Leave")
            : member.PrepareSuspend(member.MembershipEpisodeId, -1, "Leave");
        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(member.IsPrimary);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepReplacementMarkGivenDelayedOldReleaseOrMark()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register().IsSuccess);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var episode = member.MembershipEpisodeId;
        Assert.True(member.MarkPrimary(first, episode).IsSuccess);
        Assert.True(member.UnmarkPrimary(first, episode).IsSuccess);
        Assert.True(member.MarkPrimary(second, episode).IsSuccess);
        var scenario = new AggregateScenario<Member>(member);
        var before = scenario.PendingEvents.ToArray();
        // Act
        Assert.True(member.UnmarkPrimary(first, episode).IsSuccess);
        Assert.True(member.MarkPrimary(first, episode).IsSuccess);
        // Assert
        Assert.Equal(second, member.PrimaryDesignationId);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecordRefusalGivenGuestDesignation()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register("guest", new DateOnly(2027, 1, 1), programIds: [Uuid.CreateVersion4()]).IsSuccess);
        var designation = Uuid.CreateVersion4();
        // Act
        Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
        // Assert
        Assert.False(member.IsPrimary);
        Assert.IsType<Bdgrz.Compliance.Features.AccessControl.Events.MemberPrimaryMarkRefused>(member.PrimaryOutcome(designation));
    }

    [Fact]
    public void ShouldRecordExactRefusalGivenDesignationTargetsAnotherEpisode()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register().IsSuccess);
        var designation = Uuid.CreateVersion4();
        var otherEpisode = Uuid.CreateVersion4();
        var scenario = new AggregateScenario<Member>(member);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var first = member.MarkPrimary(designation, otherEpisode);
        var repeated = member.MarkPrimary(designation, otherEpisode);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(member.IsPrimary);
        var fact = Assert.IsType<Bdgrz.Compliance.Features.AccessControl.Events.MemberPrimaryMarkRefused>(
            Assert.Single(scenario.PendingEvents.Skip(before.Length)));
        Assert.Equal(otherEpisode, fact.MembershipEpisodeId);
        Assert.Equal("not_active", fact.Reason);
    }

    [Fact]
    public void ShouldRefuseEmptyUnmarkGivenNoPrimaryDesignation()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register().IsSuccess);
        var scenario = new AggregateScenario<Member>(member);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var result = member.UnmarkPrimary(Uuid.Empty, member.MembershipEpisodeId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldAllowWithdrawalGivenExactPrimaryReleaseMarkRemoved()
    {
        // Arrange
        var member = new Member(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(member.Register().IsSuccess);
        var designation = Uuid.CreateVersion4();
        Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
        Assert.True(member.UnmarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);

        // Act
        var suspension = member.PrepareSuspend(member.MembershipEpisodeId, -1, "Leaving");
        var deprovisioning = member.PrepareDeprovision(member.MembershipEpisodeId, -1, "Leaving");

        // Assert
        Assert.True(suspension.IsSuccess);
        Assert.True(deprovisioning.IsSuccess);
        Assert.False(member.IsPrimary);
    }
}
