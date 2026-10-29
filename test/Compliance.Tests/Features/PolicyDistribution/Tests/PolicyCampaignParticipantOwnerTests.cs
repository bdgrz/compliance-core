using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

/// <summary>Direct participant admission decisions for one campaign person.</summary>
public sealed class PolicyCampaignParticipantOwnerTests
{
    [Theory]
    [InlineData("ack", "identity")]
    [InlineData("ack", "version")]
    [InlineData("ack", "cause")]
    [InlineData("ack", "source")]
    [InlineData("ack", "kind")]
    [InlineData("ack", "actor")]
    [InlineData("ack", "digest")]
    [InlineData("ack", "episode")]
    [InlineData("ack", "accepted_at")]
    [InlineData("ack", "intent")]
    [InlineData("ack", "result")]
    [InlineData("completion", "identity")]
    [InlineData("completion", "version")]
    [InlineData("completion", "cause")]
    [InlineData("completion", "source")]
    [InlineData("waiver", "member")]
    [InlineData("waiver", "cause")]
    [InlineData("waiver", "source")]
    public void ShouldRefuseInvalidResultWithoutFactGivenFreshParticipant(string stage, string defect)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var campaign = Uuid.CreateVersion4();
        var person = Uuid.CreateVersion4();
        var owner = new PolicyCampaignParticipant(tenant, campaign, person);
        var scenario = new AggregateScenario<PolicyCampaignParticipant>(owner);
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Recorder");
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(ApplyPolicyParticipantResult), 0) with
        {
            LogicalId = Uuid.CreateVersion4(),
            CausationEventId = Uuid.CreateVersion4(),
            Actor = actor,
            FirstOwner = new PolicyDistributionCampaign(tenant, campaign).Stream,
        };
        operation = defect switch
        {
            "cause" => operation with { CausationEventId = null },
            "source" => operation with { FirstOwner = new PolicyDistributionCampaign(tenant, Uuid.CreateVersion4()).Stream },
            "kind" => operation with { CommandKind = "Other" },
            "actor" => operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other") },
            "digest" => operation with { AuthoredIntentSha256 = "bad" },
            "episode" => operation with { Episode = " " },
            "accepted_at" => operation with { AcceptedAt = default },
            _ => operation,
        };
        var sourceId = defect == "identity" ? Uuid.Empty : Uuid.CreateVersion4();
        var version = defect == "version" ? 0 : 1;
        var intent = NativeFacts.Ref(defect == "intent" ? "wrong" : ChildAdmissionIntent.Schema);
        var result = NativeFacts.Ref(defect == "result" ? "wrong" : "mutation-result-content.v1");
        var approverMember = defect == "member" ? Uuid.CreateVersion4() : member;

        // Act
        var outcome = stage switch
        {
            "ack" => owner.RecordAcknowledgement(operation, sourceId, version, new string('a', 64), "Acknowledged",
                actor, actor, false, NativeFacts.Now, intent, result),
            "completion" => owner.RecordCompletion(operation, sourceId, version, new DateOnly(2026, 9, 30),
                "manual", "evidence", actor, NativeFacts.Now, intent, result),
            _ => owner.ApproveWaiver(operation, "Reason", new DateOnly(2027, 9, 30), actor, approverMember,
                NativeFacts.Now, intent, result),
        };

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(0L, owner.Revision);
    }

    [Theory]
    [InlineData("ack")]
    [InlineData("completion")]
    [InlineData("waiver")]
    public void ShouldRecordOneResultGivenValidAdmittedChild(string stage)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var campaign = Uuid.CreateVersion4();
        var owner = new PolicyCampaignParticipant(tenant, campaign, Uuid.CreateVersion4());
        var scenario = new AggregateScenario<PolicyCampaignParticipant>(owner);
        var member = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(member, "Recorder");
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(ApplyPolicyParticipantResult), 0) with
        {
            LogicalId = Uuid.CreateVersion4(),
            CausationEventId = Uuid.CreateVersion4(),
            Actor = actor,
            FirstOwner = new PolicyDistributionCampaign(tenant, campaign).Stream,
        };
        var intent = NativeFacts.Ref(ChildAdmissionIntent.Schema);
        var result = NativeFacts.Ref("mutation-result-content.v1");

        // Act
        var outcome = stage switch
        {
            "ack" => owner.RecordAcknowledgement(operation, Uuid.CreateVersion4(), 1, new string('a', 64),
                "Acknowledged", actor, actor, false, NativeFacts.Now, intent, result),
            "completion" => owner.RecordCompletion(operation, Uuid.CreateVersion4(), 1, new DateOnly(2026, 9, 30),
                "manual", "evidence", actor, NativeFacts.Now, intent, result),
            _ => owner.ApproveWaiver(operation, "Reason", new DateOnly(2027, 9, 30), actor, member,
                NativeFacts.Now, intent, result),
        };

        // Assert
        Assert.True(outcome.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.Equal(1L, owner.Revision);
    }
}
