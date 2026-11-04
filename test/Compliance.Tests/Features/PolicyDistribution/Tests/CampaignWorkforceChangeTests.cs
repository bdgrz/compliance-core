using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class CampaignWorkforceChangeTests
{
    [Theory]
    [InlineData("started", "joiner", 30)]
    [InlineData("department", "mover_in", 5)]
    public void ShouldUseSourceKindDueRuleGivenNewAudienceInclusion(string sourceKind, string reason, int days)
    {
        // Arrange
        var person = Uuid.CreateVersion4();
        var today = new DateOnly(2026, 11, 4);
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [person] = new(person, "Worker", "employee", "Engineering", today),
        }, new HashSet<Uuid> { person });

        // Act
        var amendment = CampaignWorkforceChangeRules.Plan(person, "Worker", null, audience,
            sourceKind, today.AddDays(5));

        // Assert
        Assert.NotNull(amendment);
        Assert.Equal(reason, amendment.Reason);
        Assert.Equal(today.AddDays(days), amendment.DueOn);
    }

    [Theory]
    [InlineData(true, "mover_out")]
    [InlineData(false, "leaver")]
    public void ShouldKeepRemovalReasonGivenCurrentPersonPresence(bool stillPresent, string reason)
    {
        // Arrange
        var person = Uuid.CreateVersion4();
        var today = new DateOnly(2026, 11, 4);
        var participant = new CampaignParticipant(person, "Worker", "employee", "Engineering", true,
            "launch", Uuid.CreateVersion4(), today, new NativeFactReference(
                new EventStreamAddress("tenant", "campaign", "id"), Uuid.CreateVersion4(), 1));
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>(),
            stillPresent ? new HashSet<Uuid> { person } : new HashSet<Uuid>());

        // Act
        var amendment = CampaignWorkforceChangeRules.Plan(person, "Worker", participant, audience,
            "ended", today);

        // Assert
        Assert.Equal(reason, amendment!.Reason);
        Assert.Null(amendment.DueOn);
    }

    [Fact]
    public void ShouldProduceNoAmendmentGivenPersonStillMatchesAudience()
    {
        // Arrange
        var person = Uuid.CreateVersion4();
        var today = new DateOnly(2026, 11, 4);
        var participant = new CampaignParticipant(person, "Worker", "employee", "Engineering", true,
            "launch", Uuid.CreateVersion4(), today, new NativeFactReference(
                new EventStreamAddress("tenant", "campaign", "id"), Uuid.CreateVersion4(), 1));
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [person] = new(person, "Worker", "employee", "Engineering", today),
        }, new HashSet<Uuid> { person });

        // Act
        var amendment = CampaignWorkforceChangeRules.Plan(person, "Worker", participant, audience,
            "department", today);

        // Assert
        Assert.Null(amendment);
    }
}
