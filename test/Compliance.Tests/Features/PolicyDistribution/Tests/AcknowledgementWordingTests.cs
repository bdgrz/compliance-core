using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning.Models;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class AcknowledgementWordingTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldUseProductDefaultGivenNoOrganizationChoice()
    {
        // Arrange
        var owner = new AcknowledgementWording(Uuid.CreateVersion4());

        // Act
        var wording = owner.Wording;

        // Assert
        Assert.Equal(PolicyDistributionCampaign.DefaultAcknowledgementText, wording);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldKeepEveryChoiceAndNoOpGivenUnchangedWording()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var owner = new AcknowledgementWording(tenant);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var request = new SetAcknowledgementWording(Uuid.CreateVersion4(), tenant, 0, "I read and accept this policy.");
        var scenario = new AggregateScenario<AcknowledgementWording>(owner);

        // Act
        var changed = owner.Set(request, actor, Now);
        var repeated = owner.Set(request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 1 }, actor, Now);

        // Assert
        Assert.Null(changed);
        Assert.Null(repeated);
        var fact = Assert.IsType<AcknowledgementWordingSet>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(request.Wording, fact.Wording);
        Assert.Equal(actor, fact.SetBy);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReplayOriginalCommandWithoutRevertingLaterChoiceGivenSelectedCommand()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var owner = new AcknowledgementWording(tenant, command);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var first = new SetAcknowledgementWording(command, tenant, 0, "First wording");
        Assert.Null(owner.Set(first, actor, Now));
        Assert.Null(owner.Set(new SetAcknowledgementWording(Uuid.CreateVersion4(), tenant, 1,
            "Later wording"), actor, Now.AddMinutes(1)));
        var before = new AggregateScenario<AcknowledgementWording>(owner).PendingEvents.Count;

        // Act
        var retry = owner.Set(first, actor, Now.AddMinutes(2));
        var changed = owner.Set(first with { Wording = "Changed intent" }, actor, Now.AddMinutes(2));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, changed!.Code);
        Assert.Equal("Later wording", owner.Wording);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(before, new AggregateScenario<AcknowledgementWording>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRetainCapturedWordingGivenOrganizationLaterChangesItsChoice()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Org Admin");
        var wording = new AcknowledgementWording(tenant);
        Assert.Null(wording.Set(new SetAcknowledgementWording(Uuid.CreateVersion4(), tenant, 0,
            "First organization wording"), actor, Now));
        var campaign = new PolicyDistributionCampaign(tenant, Uuid.CreateVersion4());
        var operation = NativeFacts.Root(campaign, campaign.Id, "LaunchPolicyCampaign", null, member);
        var launch = campaign.LaunchCampaign(operation, Uuid.CreateVersion4(),
            new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-01", "Policy", 1, new string('a', 64)),
            "core_security", NativeFacts.Ref(), Uuid.CreateVersion4(), new string('b', 64),
            DateOnly.FromDateTime(Now.UtcDateTime), DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30),
            null, 0, operation.Actor, member, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), wording.Wording);
        Assert.True(launch.IsSuccess, launch.Error?.Message);

        // Act
        var changed = wording.Set(new SetAcknowledgementWording(Uuid.CreateVersion4(), tenant, 1,
            "Later organization wording"), actor, Now.AddMinutes(1));

        // Assert
        Assert.Null(changed);
        var fact = Assert.IsType<PolicyCampaignLaunched>(
            new AggregateScenario<PolicyDistributionCampaign>(campaign).PendingEvents[0]);
        Assert.Equal("First organization wording", fact.AcknowledgementText);
        Assert.Equal("Later organization wording", wording.Wording);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldRefuseWithoutFactGivenMissingWording(string text)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var owner = new AcknowledgementWording(tenant);
        var request = new SetAcknowledgementWording(Uuid.CreateVersion4(), tenant, 0, text);

        // Act
        var failure = owner.Set(request, ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin"), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
        Assert.Equal(0, owner.Revision);
        Assert.Empty(new AggregateScenario<AcknowledgementWording>(owner).PendingEvents);
    }
}
