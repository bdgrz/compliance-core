using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

/// <summary>Owner-level decisions of one control's operating plan line, without handlers or content stores.</summary>
public sealed class ControlOperatingPlanAggregateTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Control = Uuid.CreateVersion4();

    [Fact]
    public void ShouldAddressStreamByTenantProgramAndControlGivenAnyOpener()
    {
        // Arrange
        var first = Fresh();
        var selected = Fresh(Uuid.CreateVersion4());

        // Act
        var other = new ControlOperatingPlan(Tenant, Program, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, selected.Id);
        Assert.Equal(first.Stream, selected.Stream);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal(ControlOperatingPlan.Area, first.Stream.Area);
    }

    [Fact]
    public void ShouldRaiseProposalGivenEmptyLineAndRefuseSecondWhilePending()
    {
        // Arrange
        var owner = Fresh();
        var planVersionId = Uuid.CreateVersion4();

        // Act
        var proposed = Propose(owner, planVersionId, 0);
        var second = Propose(owner, Uuid.CreateVersion4(), 1);

        // Assert
        Assert.True(proposed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        var fact = Assert.IsType<ControlOperatingPlanProposed>(Assert.Single(Pending(owner)));
        Assert.Equal(1, fact.Revision);
        Assert.Equal(planVersionId, fact.Operation.LogicalId);
        Assert.Equal(planVersionId, owner.PendingPlanVersionId);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenStaleExpectedRevisionOnProposal()
    {
        // Arrange
        var owner = Fresh();

        // Act
        var result = Propose(owner, Uuid.CreateVersion4(), 3);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenProposalWithoutPlanVersionIdentity()
    {
        // Arrange
        var owner = Fresh();

        // Act
        var result = Propose(owner, Uuid.Empty, 0);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Pending(owner));
        Assert.Null(owner.PendingPlanVersionId);
    }

    [Fact]
    public void ShouldRefuseGivenOperationNotRootedOnOwnStreamOrWithoutExpectedRevision()
    {
        // Arrange
        var owner = Fresh();
        var other = new ControlOperatingPlan(Tenant, Program, Uuid.CreateVersion4());
        var planVersionId = Uuid.CreateVersion4();
        Assert.True(Propose(owner, planVersionId, 0).IsSuccess);

        // Act
        var foreign = owner.Approve(Root(other, planVersionId, "ApproveControlOperatingPlan", 1), planVersionId,
            Uuid.CreateVersion4(), Approver(), NativeFacts.Now, "ok", null, NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref());
        var unversioned = owner.Approve(Root(owner, Uuid.CreateVersion4(), "ApproveControlOperatingPlan", null),
            planVersionId, Uuid.CreateVersion4(), Approver(), NativeFacts.Now, "ok", null, NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreign.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unversioned.Error?.Kind);
        Assert.Single(Pending(owner));
    }

    [Fact]
    public void ShouldAppendNothingGivenApprovalWithoutPendingPlanOrOfAnotherVersion()
    {
        // Arrange
        var owner = Fresh();

        // Act
        var none = Approve(owner, Uuid.CreateVersion4(), 0);
        var planVersionId = Uuid.CreateVersion4();
        Assert.True(Propose(owner, planVersionId, 0).IsSuccess);
        var wrongVersion = Approve(owner, Uuid.CreateVersion4(), 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, none.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongVersion.Error?.Kind);
        Assert.Single(Pending(owner));
        Assert.Equal(planVersionId, owner.PendingPlanVersionId);
    }

    [Fact]
    public void ShouldAppendNothingGivenStaleExpectedRevisionOnApproval()
    {
        // Arrange
        var owner = Fresh();
        var planVersionId = Uuid.CreateVersion4();
        Assert.True(Propose(owner, planVersionId, 0).IsSuccess);

        // Act
        var stale = Approve(owner, planVersionId, 0);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Single(Pending(owner));
        Assert.Equal(planVersionId, owner.PendingPlanVersionId);
    }

    [Fact]
    public void ShouldClearPendingGivenApprovalAndAllowNextProposal()
    {
        // Arrange
        var owner = Fresh();
        var first = Uuid.CreateVersion4();
        Assert.True(Propose(owner, first, 0).IsSuccess);

        // Act
        var approved = Approve(owner, first, 1);
        var next = Propose(owner, Uuid.CreateVersion4(), 2);
        var again = Approve(owner, first, 3);

        // Assert
        Assert.True(approved.IsSuccess);
        Assert.True(next.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        long[] expected = [1L, 2L, 3L];
        Assert.Equal(expected, Pending(owner).Select(RevisionOf).ToArray());
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreStateGivenValidHistoryAndRefuseStaleCommandAfterReplay()
    {
        // Arrange
        var seed = Fresh();
        var planVersionId = Uuid.CreateVersion4();
        Assert.True(Propose(seed, planVersionId, 0).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var replayed = NativeFacts.Replay(Fresh(), history);
        var revision = replayed.Revision;
        var pending = replayed.PendingPlanVersionId;
        var stale = Approve(replayed, planVersionId, 0);
        var current = Approve(replayed, planVersionId, 1);

        // Assert
        Assert.Equal(1, revision);
        Assert.Equal(planVersionId, pending);
        Assert.False(stale.IsSuccess);
        Assert.True(current.IsSuccess);
        Assert.Null(replayed.PendingPlanVersionId);
    }

    [Fact]
    public void ShouldRecognizeExactRetryAndRefuseChangedIntentGivenCommittedFactAfterLaterFacts()
    {
        // Arrange
        var seed = Fresh();
        var planVersionId = Uuid.CreateVersion4();
        var proposal = Root(seed, planVersionId, "ProposeControlOperatingPlan", 0);
        Assert.True(seed.Propose(proposal, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        Assert.True(Approve(seed, planVersionId, 1).IsSuccess);
        Assert.True(Propose(seed, Uuid.CreateVersion4(), 2).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var replayed = NativeFacts.Replay(Fresh(planVersionId), history);
        var exact = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(proposal));
        var changed = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(proposal with
        {
            AuthoredIntentSha256 = new string('c', 64),
        }));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(3, replayed.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenAlreadyAcceptedPlanVersionIdentityOnNewProposalOrApproval()
    {
        // Arrange
        var seed = Fresh();
        var planVersionId = Uuid.CreateVersion4();
        Assert.True(Propose(seed, planVersionId, 0).IsSuccess);
        var history = NativeFacts.Raised(seed);
        var replayed = NativeFacts.Replay(Fresh(planVersionId), history);

        // Act
        var approvalSharingProposalId = replayed.Approve(
            Root(replayed, planVersionId, "ApproveControlOperatingPlan", 1), planVersionId, Uuid.CreateVersion4(),
            Approver(), NativeFacts.Now, "ok", null, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        Assert.True(Approve(replayed, planVersionId, 1).IsSuccess);
        var reusedProposal = Propose(replayed, planVersionId, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, approvalSharingProposalId.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reusedProposal.Error?.Kind);
        Assert.Single(Pending(replayed));
    }

    static ControlOperatingPlan Fresh(Uuid selectedLogicalId = default) =>
        new(Tenant, Program, Control, selectedLogicalId);

    static AggregateScenario<ControlOperatingPlan> Scenario(ControlOperatingPlan owner) => new(owner);

    static DomainEvent[] Pending(ControlOperatingPlan owner) => Scenario(owner).PendingEvents.ToArray();

    static long RevisionOf(DomainEvent fact) => fact switch
    {
        ControlOperatingPlanProposed proposed => proposed.Revision,
        ControlOperatingPlanApproved approved => approved.Revision,
        _ => throw new InvalidOperationException("Not an operating plan fact."),
    };

    static ActorReference Approver() => ActorReference.ForMember(Uuid.CreateVersion4(), "Approver");

    static OperationProvenance Root(Aggregate owner, Uuid logicalId, string commandKind, long? expectedRevision) =>
        NativeFacts.Root(owner, logicalId, commandKind, expectedRevision);

    static Result Propose(ControlOperatingPlan owner, Uuid planVersionId, long expectedRevision) =>
        owner.Propose(Root(owner, planVersionId, "ProposeControlOperatingPlan", expectedRevision),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

    static Result Approve(ControlOperatingPlan owner, Uuid planVersionId, long expectedRevision) =>
        owner.Approve(Root(owner, Uuid.CreateVersion4(), "ApproveControlOperatingPlan", expectedRevision),
            planVersionId, Uuid.CreateVersion4(), Approver(), NativeFacts.Now, "Looks right.", null,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
}
