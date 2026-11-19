using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.FirmServices.Tests;

public sealed class PartnerDesignationTests
{
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid OperatorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainGrantAndRevocationGivenPartnerRedesignated()
    {
        // Arrange
        var owner = new PartnerDesignation(UserId, "engagement_partner");
        Assert.True(owner.Designate(new(Uuid.CreateVersion4(), UserId, "engagement_partner", 0, "Qualified"),
            OperatorId, Now).IsSuccess);
        Assert.True(owner.Revoke(new(Uuid.CreateVersion4(), UserId, "engagement_partner", 1, "Duty ended"),
            OperatorId, Now.AddDays(1)).IsSuccess);

        // Act
        var result = owner.Designate(new(Uuid.CreateVersion4(), UserId, "engagement_partner", 2, "Reappointed"),
            OperatorId, Now.AddDays(2));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(owner.View!.IsDesignated);
        Assert.Equal(3, owner.Revision);
        Assert.Equal(3, new AggregateScenario<PartnerDesignation>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldReplayOriginalGrantGivenLaterRevocation()
    {
        // Arrange
        var request = new DesignatePartner(Uuid.CreateVersion4(), UserId, "quality_management_partner", 0, "Qualified");
        var source = new PartnerDesignation(UserId, request.Duty);
        var original = source.Designate(request, OperatorId, Now);
        Assert.True(source.Revoke(new(Uuid.CreateVersion4(), UserId, request.Duty, 1, "Duty ended"),
            OperatorId, Now.AddDays(1)).IsSuccess);
        var owner = NativeFacts.Replay(new PartnerDesignation(UserId, request.Duty, request.CommandId),
            NativeFacts.Raised(source));

        // Act
        var retry = owner.Designate(request, OperatorId, Now.AddDays(2));
        var changed = owner.Designate(request with { Reason = "Changed" }, OperatorId, Now.AddDays(2));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(original.Value, retry.Value);
        Assert.False(changed.IsSuccess);
        Assert.False(owner.View!.IsDesignated);
        Assert.Empty(new AggregateScenario<PartnerDesignation>(owner).PendingEvents);
    }

    [Theory]
    [InlineData("rule_ratifier")]
    [InlineData("unknown")]
    public void ShouldRefuseWithoutFactGivenUnknownDuty(string duty)
    {
        // Arrange
        var owner = new PartnerDesignation(UserId, duty);

        // Act
        var result = owner.Designate(new(Uuid.CreateVersion4(), UserId, duty, 0, "Qualified"), OperatorId, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(owner.View);
        Assert.Empty(new AggregateScenario<PartnerDesignation>(owner).PendingEvents);
    }
}
