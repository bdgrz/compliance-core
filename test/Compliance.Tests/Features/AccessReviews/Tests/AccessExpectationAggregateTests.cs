using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>Owner-level tests of the expectation line: guards, refusals that append nothing, and valid replay.</summary>
public sealed class AccessExpectationAggregateTests
{
    [Fact]
    public void ShouldRaiseRevisionOneProposalGivenNewLineRootedInItsFirstProposal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var owner = new AccessExpectation(tenantId, lineId, lineId, lineId);

        // Act
        var result = owner.Propose(NativeFacts.Root(owner, lineId, "ProposeAccessExpectation", 0), instanceId,
            Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.IsType<AccessExpectationProposed>(
            Assert.Single(new AggregateScenario<AccessExpectation>(owner).PendingEvents));
        Assert.Equal(1, raised.Revision);
        Assert.Equal(tenantId, raised.TenantId);
        Assert.Equal(instanceId, raised.SystemInstanceId);
        Assert.Equal(lineId, raised.Operation.LogicalId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.True(owner.Contains(lineId));
        Assert.False(owner.IsApproved(lineId));
    }

    [Fact]
    public void ShouldAppendNothingGivenProposalNotRootedInLineIdentityOrWithoutOwnRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var otherLogicalId = Uuid.CreateVersion4();
        var owner = new AccessExpectation(tenantId, lineId, otherLogicalId, otherLogicalId);
        var foreign = new AccessExpectation(tenantId, Uuid.CreateVersion4(), lineId, lineId);

        // Act
        var rootedElsewhere = owner.Propose(NativeFacts.Root(owner, otherLogicalId, "ProposeAccessExpectation", 0),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var foreignOwner = owner.Propose(NativeFacts.Root(foreign, otherLogicalId, "ProposeAccessExpectation", 0),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var withoutRevision = owner.Propose(NativeFacts.Root(owner, otherLogicalId, "ProposeAccessExpectation", null),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, rootedElsewhere.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, foreignOwner.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessExpectation>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.SystemInstanceId);
    }

    [Fact]
    public void ShouldAppendNothingGivenStaleExpectedRevisionOnNewLine()
    {
        // Arrange
        var lineId = Uuid.CreateVersion4();
        var owner = new AccessExpectation(Uuid.CreateVersion4(), lineId, lineId, lineId);

        // Act
        var result = owner.Propose(NativeFacts.Root(owner, lineId, "ProposeAccessExpectation", 3),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessExpectation>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenApprovalOrExceptionOnLineWithoutFacts()
    {
        // Arrange
        var lineId = Uuid.CreateVersion4();
        var owner = new AccessExpectation(Uuid.CreateVersion4(), lineId, lineId, lineId);
        var approver = ActorReference.ForMember(Uuid.CreateVersion4(), "Approver");

        // Act
        var approval = owner.Approve(NativeFacts.Root(owner, lineId, "ApproveAccessExpectation", 0), lineId, approver,
            NativeFacts.Now, null, NativeFacts.Ref(), NativeFacts.Ref());
        var exception = owner.RecordException(NativeFacts.Root(owner, lineId, "ExemptAccessExpectation", 0), lineId,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, approval.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, exception.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessExpectation>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreLineStateGivenValidProposedApprovedAndExceptionHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var facts = History(tenantId, lineId, instanceId, Uuid.CreateVersion4(), 3);

        // Act
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId), facts);

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.Equal(tenantId, owner.TenantId);
        Assert.True(owner.Contains(lineId));
        Assert.True(owner.IsApproved(lineId));
        Assert.IsType<AccessExpectationExceptionRecorded>(owner.Latest);
    }

    [Fact]
    public void ShouldSelectNothingGivenHistoryHydratedForAnotherExpectation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var facts = History(tenantId, lineId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2);

        // Act
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, Uuid.CreateVersion4()), facts);

        // Assert
        Assert.Equal(2, owner.Revision);
        Assert.False(owner.Contains(lineId));
        Assert.False(owner.IsApproved(lineId));
        Assert.Null(owner.SelectedProposal);
    }

    [Fact]
    public void ShouldFindCommittedApprovalGivenSelectedOperationAfterLaterException()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var facts = History(tenantId, lineId, Uuid.CreateVersion4(), approvalId, 3);

        // Act
        var retried = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, approvalId, lineId), facts);
        var unaccepted = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.CreateVersion4(), lineId), facts);

        // Assert
        Assert.IsType<AccessExpectationApproved>(retried.Acceptance.SelectedFact);
        Assert.Equal(approvalId, retried.Acceptance.SelectedProvenance!.LogicalId);
        Assert.Equal(3, retried.Revision);
        Assert.Null(unaccepted.Acceptance.SelectedFact);
    }

    [Fact]
    public void ShouldPrepareApprovalOnlyGivenProposedExpectationAtCurrentRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId),
            History(tenantId, lineId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1));

        // Act
        var current = owner.PrepareApproval(1, lineId);
        var stale = owner.PrepareApproval(0, lineId);
        var unknown = owner.PrepareApproval(1, Uuid.CreateVersion4());

        // Assert
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseSecondApprovalAndPrepareExceptionGivenApprovedExpectation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId),
            History(tenantId, lineId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2));

        // Act
        var secondApproval = owner.PrepareApproval(2, lineId);
        var exception = owner.PrepareException(2, lineId);
        var staleException = owner.PrepareException(1, lineId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, secondApproval.Error?.Kind);
        Assert.True(exception.IsSuccess, exception.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, staleException.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseExceptionGivenProposedButUnapprovedExpectation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId),
            History(tenantId, lineId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1));

        // Act
        var result = owner.PrepareException(1, lineId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public void ShouldPrepareProposalOnlyGivenNewExpectationOnSameSystemInstanceAtCurrentRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId),
            History(tenantId, lineId, instanceId, Uuid.CreateVersion4(), 1));

        // Act
        var successor = owner.PrepareProposal(1, instanceId, Uuid.CreateVersion4());
        var existing = owner.PrepareProposal(1, instanceId, lineId);
        var otherInstance = owner.PrepareProposal(1, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var stale = owner.PrepareProposal(0, instanceId, Uuid.CreateVersion4());

        // Assert
        Assert.True(successor.IsSuccess, successor.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, existing.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherInstance.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
    }

    [Fact]
    public void ShouldAppendNothingGivenApprovalOnReplayedLineWithoutFinalizedCut()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var lineId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessExpectation(tenantId, lineId, Uuid.Empty, lineId),
            History(tenantId, lineId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1));
        var approver = ActorReference.ForMember(Uuid.CreateVersion4(), "Approver");

        // Act
        var result = owner.Approve(NativeFacts.Root(owner, Uuid.CreateVersion4(), "ApproveAccessExpectation", 1),
            lineId, approver, NativeFacts.Now, null, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessExpectation>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    static DomainEvent[] History(Uuid tenantId, Uuid lineId, Uuid instanceId, Uuid approvalId, int count)
    {
        var author = new AccessExpectation(tenantId, lineId, lineId, lineId);
        var proposed = author.Propose(NativeFacts.Root(author, lineId, "ProposeAccessExpectation", 0), instanceId,
            Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var facts = new List<DomainEvent>(NativeFacts.Raised(author))
        {
            DomainEventSeed.Attach(new AccessExpectationApproved(tenantId, instanceId, 2, lineId,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Approver"), NativeFacts.Now, null,
                NativeFacts.Root(author, approvalId, "ApproveAccessExpectation", 1), NativeFacts.Ref(),
                NativeFacts.Ref()), lineId, 2),
            DomainEventSeed.Attach(new AccessExpectationExceptionRecorded(tenantId, instanceId, 3, NativeFacts.Ref(),
                NativeFacts.Root(author, Uuid.CreateVersion4(), "ExemptAccessExpectation", 2), NativeFacts.Ref(),
                NativeFacts.Ref()), lineId, 3)
        };
        return facts.Take(count).ToArray();
    }
}
