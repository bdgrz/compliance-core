using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

/// <summary>Owner-level admission checks for one contribution.</summary>
public sealed class MethodologyTemplateContributionOwnerTests
{
    [Theory]
    [InlineData("propose", "user")]
    [InlineData("propose", "member")]
    [InlineData("propose", "owner")]
    [InlineData("propose", "root")]
    [InlineData("propose", "kind")]
    [InlineData("review", "user")]
    [InlineData("review", "member")]
    [InlineData("review", "cause")]
    [InlineData("review", "time")]
    [InlineData("accept", "user")]
    [InlineData("accept", "member")]
    [InlineData("accept", "kind")]
    public void ShouldRefuseWithoutFactGivenInvalidNativeAdmission(string stage, string defect)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var owner = new MethodologyTemplateContribution(tenant, id);
        var scenario = new AggregateScenario<MethodologyTemplateContribution>(owner);
        var proposer = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var accepting = Uuid.CreateVersion4();
        var content = NativeFacts.Ref();
        if (stage != "propose")
        {
            var proposed = Provenance(owner, proposer, tenant, false, id, nameof(ProposeDeidentifiedTemplateContribution), NativeFacts.Now);
            Assert.True(owner.Propose(proposed, RbacIds.Member(tenant, proposer), proposer, content, content, content).IsSuccess);
        }
        if (stage == "accept")
        {
            var reviewed = Provenance(owner, reviewer, tenant, true, Uuid.CreateVersion4(), nameof(ReviewTemplateContribution), NativeFacts.Now.AddMinutes(1));
            Assert.True(owner.Review(reviewed, RbacIds.Member(Uuid.Empty, reviewer), reviewer, "approved", content, content, content).IsSuccess);
        }
        var user = stage == "propose" ? proposer : stage == "review" ? reviewer : accepting;
        var platform = stage != "propose";
        var kind = stage == "propose" ? nameof(ProposeDeidentifiedTemplateContribution)
            : stage == "review" ? nameof(ReviewTemplateContribution) : nameof(AcceptTemplateContribution);
        var command = stage == "propose" ? id : Uuid.CreateVersion4();
        var operation = Provenance(owner, user, tenant, platform, command, kind, NativeFacts.Now.AddMinutes(2));
        var member = RbacIds.Member(platform ? Uuid.Empty : tenant, user);
        var foreign = new MethodologyTemplateContribution(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;
        operation = defect switch
        {
            "owner" => operation with { Owner = foreign },
            "root" => operation with { RootOperationId = Uuid.CreateVersion4() },
            "kind" => operation with { CommandKind = "Other" },
            "cause" => operation with { CausationEventId = Uuid.CreateVersion4() },
            "time" => operation with { AcceptedAt = NativeFacts.Now.AddMinutes(-1) },
            _ => operation,
        };
        if (defect == "user")
            user = Uuid.Empty;
        if (defect == "member")
            member = Uuid.CreateVersion4();
        var before = scenario.PendingEvents.Count;
        var priorRevision = owner.Revision;

        // Act
        var outcome = stage == "propose" ? owner.Propose(operation, member, user, content, content, content)
            : stage == "review" ? owner.Review(operation, member, user, "approved", content, content, content)
            : owner.Accept(operation, member, user, content, content, content);

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents.Count);
        Assert.Equal(priorRevision, owner.Revision);
    }

    static OperationProvenance Provenance(MethodologyTemplateContribution owner, Uuid user, Uuid tenant, bool platform,
        Uuid command, string kind, DateTimeOffset at) => OwnerProvenance.For(owner, command, kind,
        platform ? ActorReference.ForPlatformOperator(user, "Operator") : ActorReference.ForMember(RbacIds.Member(tenant, user), "Member"),
        owner.Id.ToString(), null, new string('a', 64), at);
}
