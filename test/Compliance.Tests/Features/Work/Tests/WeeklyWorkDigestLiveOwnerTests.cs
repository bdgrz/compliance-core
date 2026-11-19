using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyWorkDigestLiveOwnerTests
{
    static readonly DateOnly Week = new(2026, 11, 2);
    static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepIndependentOwnerGivenDifferentMemberWeekOrTenant()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();

        // Act
        var first = WeeklyWorkDigest.ForDelivery(tenant, member, Week);
        var otherWeek = WeeklyWorkDigest.ForDelivery(tenant, member, Week.AddDays(7));
        var otherMember = WeeklyWorkDigest.ForDelivery(tenant, Uuid.CreateVersion4(), Week);
        var otherTenant = WeeklyWorkDigest.ForDelivery(Uuid.CreateVersion4(), member, Week);

        // Assert
        Assert.NotEqual(first.Stream, otherWeek.Stream);
        Assert.NotEqual(first.Stream, otherMember.Stream);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
    }

    [Fact]
    public void ShouldFreezeSentFactsAndPreventSecondOutcomeGivenSuccessfulDelivery()
    {
        // Arrange
        var owner = NewOwner();
        Assert.True(owner.ScheduleDelivery(new WorkDigestScheduleWindow(Week, "UTC", Now), TimeSpan.FromDays(6), Now));
        Assert.True(owner.StartDeliveryAttempt(Now));
        var key = Uuid.CreateVersion4();
        var message = new WorkDigestDeliveryMessage(owner.ReadDelivery().MessageId, "member@example.com", "Digest", "Work", [key]);

        // Act
        var sent = owner.RecordWeeklySent(message, Now.AddSeconds(1));
        var repeated = owner.RecordWeeklySent(message, Now.AddSeconds(2));
        var skipped = owner.RecordWeeklySkipped("empty", Now.AddSeconds(3));

        // Assert
        Assert.True(sent);
        Assert.False(repeated);
        Assert.False(skipped);
        Assert.Equal("sent", owner.ReadDelivery().Status);
        var fact = Assert.Single(new AggregateScenario<WeeklyWorkDigest>(owner).PendingEvents.OfType<WeeklyWorkDigestSent>());
        Assert.Equal([key], fact.ItemKeys);
        Assert.Equal("member@example.com", fact.Destination);
        Assert.Equal("UTC", fact.TimeZoneId);
        Assert.Equal(Week, fact.WeekOf);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("opted_out")]
    [InlineData("no_verified_email")]
    [InlineData("inactive_member")]
    public void ShouldRecordOneSkippedFactGivenFailedCurrentEligibility(string reason)
    {
        // Arrange
        var owner = NewOwner();
        Assert.True(owner.ScheduleDelivery(new WorkDigestScheduleWindow(Week, "UTC", Now), TimeSpan.FromDays(6), Now));

        // Act
        var first = owner.RecordWeeklySkipped(reason, Now);
        var repeated = owner.RecordWeeklySkipped(reason, Now.AddSeconds(1));

        // Assert
        Assert.True(first);
        Assert.False(repeated);
        var fact = Assert.Single(new AggregateScenario<WeeklyWorkDigest>(owner).PendingEvents.OfType<WeeklyWorkDigestSkipped>());
        Assert.Equal(reason, fact.Reason);
        Assert.Equal("skipped", owner.ReadDelivery().Status);
    }

    static WeeklyWorkDigest NewOwner() => WeeklyWorkDigest.ForDelivery(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Week);
}
