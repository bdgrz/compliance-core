using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentActionAggregateTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = Uuid.CreateVersion4();
    static readonly Uuid ActionId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldDeriveDistinctIdentityGivenDifferentRiskOrAction()
    {
        // Arrange
        var own = RiskTreatmentAction.IdentityOf(TenantId, ProgramId, RiskId, ActionId);

        // Act
        var again = RiskTreatmentAction.IdentityOf(TenantId, ProgramId, RiskId, ActionId);
        var otherRisk = RiskTreatmentAction.IdentityOf(TenantId, ProgramId, Uuid.CreateVersion4(), ActionId);
        var otherAction = RiskTreatmentAction.IdentityOf(TenantId, ProgramId, RiskId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(own, again);
        Assert.NotEqual(own, otherRisk);
        Assert.NotEqual(own, otherAction);
    }

    [Fact]
    public void ShouldRefuseSecondAddGivenAddedActionIdentity()
    {
        // Arrange
        var action = Open();
        var second = NativeFacts.Root(action, ActionId, nameof(AddRiskTreatmentAction), 0);

        // Act
        var result = action.Add(second, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(new AggregateScenario<RiskTreatmentAction>(action).PendingEvents);
        Assert.Equal(1, action.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenAddByAnotherOperationIdentity()
    {
        // Arrange
        var action = new RiskTreatmentAction(TenantId, ProgramId, RiskId, ActionId);
        var foreign = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(AddRiskTreatmentAction), 0);

        // Act
        var result = action.Add(foreign, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<RiskTreatmentAction>(action).PendingEvents);
        Assert.False(action.IsAdded);
    }

    [Fact]
    public void ShouldAppendNothingGivenReviseOrSubmitBeforeAdd()
    {
        // Arrange
        var action = new RiskTreatmentAction(TenantId, ProgramId, RiskId, ActionId);

        // Act
        var revised = Revise(action, 0);
        var submitted = Submit(action, 0);
        var canceled = Cancel(action, 0);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, revised.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, submitted.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, canceled.Error?.Kind);
        Assert.Empty(new AggregateScenario<RiskTreatmentAction>(action).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionGivenWriteAtOlderRevision()
    {
        // Arrange
        var action = Open();
        Assert.True(Revise(action, 1).IsSuccess);

        // Act
        var stale = Revise(action, 1);
        var staleSubmit = Submit(action, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, staleSubmit.Error?.Kind);
        Assert.Equal(RiskStates.ActionOpen, action.Status);
        Assert.Equal(2, action.Revision);
        Assert.Equal(2, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseReviseAndSecondSubmitGivenSubmittedCompletion()
    {
        // Arrange
        var action = Open();
        Assert.True(Submit(action, 1).IsSuccess);

        // Act
        var revised = Revise(action, 2);
        var submittedAgain = Submit(action, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, revised.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, submittedAgain.Error?.Kind);
        Assert.Equal(RiskStates.ActionSubmitted, action.Status);
        Assert.NotNull(action.PendingSubmission);
        Assert.Equal(2, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldFenceCancellationAndReviewGivenPendingReview()
    {
        // Arrange
        var action = Open();
        Assert.True(Submit(action, 1).IsSuccess);
        var review = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(ReviewRiskTreatmentActionCompletion), 2,
            MemberId);
        Assert.True(action.AcceptReview(review, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);

        // Act
        var canceled = Cancel(action, 3);
        var secondReview = action.AcceptReview(NativeFacts.Root(action, Uuid.CreateVersion4(),
            nameof(ReviewRiskTreatmentActionCompletion), 3, MemberId), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, canceled.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, secondReview.Error?.Kind);
        Assert.NotNull(action.PendingReview);
        Assert.Equal(3, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseReviewDecisionGivenNoPendingReview()
    {
        // Arrange
        var action = Open();
        Assert.True(Submit(action, 1).IsSuccess);
        var operation = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(CompleteRiskTreatmentActionReview), 2,
            MemberId);

        // Act
        var decided = action.CompleteReview(operation, RiskStates.Accept, "Accepted.",
            ActorReference.ForMember(MemberId, "Reviewer"), NativeFacts.Now, null, NativeFacts.Ref(), NativeFacts.Ref());
        var aborted = action.CancelReview(operation, NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, decided.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, aborted.Error?.Kind);
        Assert.Equal(RiskStates.ActionSubmitted, action.Status);
        Assert.Equal(2, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldCancelSubmittedWorkGivenNoPendingReview()
    {
        // Arrange
        var action = Open();
        Assert.True(Submit(action, 1).IsSuccess);

        // Act
        var canceled = Cancel(action, 2);

        // Assert
        Assert.True(canceled.IsSuccess);
        Assert.Equal(RiskStates.ActionCancelled, action.Status);
        Assert.NotNull(action.CancelledFact);
        Assert.Equal(3, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseEveryWriteGivenCancelledAction()
    {
        // Arrange
        var action = Open();
        Assert.True(Cancel(action, 1).IsSuccess);

        // Act
        var cancelAgain = Cancel(action, 2);
        var revised = Revise(action, 2);
        var submitted = Submit(action, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, cancelAgain.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, revised.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, submitted.Error?.Kind);
        Assert.Equal(RiskStates.ActionCancelled, action.Status);
        Assert.Equal(2, new AggregateScenario<RiskTreatmentAction>(action).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseWriteGivenNonRootOrMissingExpectedRevision()
    {
        // Arrange
        var action = Open();
        var unversioned = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(ReviseRiskTreatmentAction), null,
            MemberId);

        // Act
        var result = action.Revise(unversioned, "Title", "Target", "Evidence", new DateOnly(2027, 1, 1), MemberId,
            NativeFacts.Ref(), unversioned.Actor, unversioned.AcceptedAt, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Single(new AggregateScenario<RiskTreatmentAction>(action).PendingEvents);
    }

    [Fact]
    public void ShouldReplayLifecycleGivenValidHistoryAndRefuseSameStaleWrite()
    {
        // Arrange
        var action = Open();
        Assert.True(Revise(action, 1).IsSuccess);
        Assert.True(Submit(action, 2).IsSuccess);
        var history = NativeFacts.Raised(action);

        // Act
        var replayed = NativeFacts.Replay(new RiskTreatmentAction(TenantId, ProgramId, RiskId, ActionId), history);
        var (revision, status, hasPending) = (replayed.Revision, replayed.Status, replayed.PendingSubmission is not null);
        var stale = Cancel(replayed, 2);

        // Assert
        Assert.Equal(3, revision);
        Assert.Equal(RiskStates.ActionSubmitted, status);
        Assert.True(hasPending);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RiskStates.ActionSubmitted, replayed.Status);
        Assert.Equal(3, history.Length);
    }

    static RiskTreatmentAction Open()
    {
        var action = new RiskTreatmentAction(TenantId, ProgramId, RiskId, ActionId);
        var operation = NativeFacts.Root(action, ActionId, nameof(AddRiskTreatmentAction), 0, MemberId);
        Assert.True(action.Add(operation, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        return action;
    }

    static Result Revise(RiskTreatmentAction action, long expectedRevision)
    {
        var operation = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(ReviseRiskTreatmentAction),
            expectedRevision, MemberId);
        return action.Revise(operation, "Title", "Target", "Evidence", new DateOnly(2027, 1, 1), MemberId,
            NativeFacts.Ref(), operation.Actor, operation.AcceptedAt, NativeFacts.Ref(), NativeFacts.Ref());
    }

    static Result Cancel(RiskTreatmentAction action, long expectedRevision)
    {
        var operation = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(CancelRiskTreatmentAction),
            expectedRevision, MemberId);
        return action.Cancel(operation, "No longer needed.", operation.Actor, operation.AcceptedAt, NativeFacts.Ref(),
            NativeFacts.Ref());
    }

    static Result Submit(RiskTreatmentAction action, long expectedRevision)
    {
        var operation = NativeFacts.Root(action, Uuid.CreateVersion4(), nameof(SubmitRiskTreatmentActionCompletion),
            expectedRevision, MemberId);
        return action.Submit(operation, "Done.", NativeFacts.Ref(), MemberId, operation.Actor, operation.AcceptedAt,
            NativeFacts.Ref(), NativeFacts.Ref());
    }
}
