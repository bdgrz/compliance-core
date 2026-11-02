using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     Owner-level tests of the campaign: launch, the single completion gate, bulk admission, closure and
///     cancellation guards, refusals that append nothing, exact retry after later facts, and valid replay.
/// </summary>
public sealed class AccessReviewCampaignAggregateTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Owner");

    [Fact]
    public void ShouldLaunchAtRevisionOneGivenFreshCampaign()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var owner = new AccessReviewCampaign(tenantId, campaignId, campaignId);

        // Act
        var result = Launch(owner, campaignId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var launched = Assert.IsType<AccessReviewCampaignLaunched>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal("Q3 review", launched.Name);
        Assert.Equal("Review access.", launched.Instructions);
        Assert.Equal(tenantId, launched.TenantId);
        Assert.Equal(campaignId, launched.CampaignId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(AccessReviewCampaign.Active, owner.Status);
        Assert.True(owner.IsLaunched);
    }

    [Fact]
    public void ShouldRefuseLaunchGivenOperationOfAnotherCampaign()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var owner = new AccessReviewCampaign(tenantId, campaignId, campaignId);

        // Act
        var result = Launch(owner, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.False(owner.IsLaunched);
    }

    [Fact]
    public void ShouldRefuseLaunchGivenCampaignAlreadyLaunched()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, campaignId, LaunchFact(tenantId, campaignId));

        // Act
        var result = Launch(owner, campaignId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreCompletedCampaignGivenValidLaunchAdmissionAndClosureHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();

        // Act
        var owner = Hydrate(tenantId, campaignId, Uuid.Empty, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent),
            CompletedFact(tenantId, campaignId, completionId, 3, intent));

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.True(owner.IsCompleted);
        Assert.Equal(AccessReviewCampaign.Completed, owner.Status);
        Assert.Null(owner.Pending);
        Assert.NotNull(owner.ClosedFor(completionId));
        Assert.Null(owner.ClosedFor(Uuid.CreateVersion4()));
        Assert.IsType<AccessReviewCampaignCompleted>(owner.Latest);
    }

    [Fact]
    public void ShouldAdmitCompletionAndSealCampaignGivenCurrentRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, completionId, LaunchFact(tenantId, campaignId));

        // Act
        var result = owner.AcceptCompletion(NativeFacts.Root(owner, completionId, "CompleteAccessReviewCampaign", 1),
            intent, NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var accepted = Assert.IsType<AccessReviewCampaignCompletionAccepted>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal(2, accepted.Revision);
        Assert.Equal(intent, accepted.Intent);
        Assert.Equal(owner.Stream, accepted.Owner);
        Assert.Equal(accepted.Revision, owner.Pending?.Revision);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(AccessReviewCampaign.Active, owner.Status);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenStaleExpectedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, completionId, LaunchFact(tenantId, campaignId),
            BulkFact(tenantId, campaignId, Uuid.CreateVersion4(), 2));

        // Act
        var result = owner.AcceptCompletion(NativeFacts.Root(owner, completionId, "CompleteAccessReviewCampaign", 1),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenCampaignNeverLaunched()
    {
        // Arrange
        var completionId = Uuid.CreateVersion4();
        var owner = new AccessReviewCampaign(Uuid.CreateVersion4(), Uuid.CreateVersion4(), completionId);

        // Act
        var result = owner.AcceptCompletion(NativeFacts.Root(owner, completionId, "CompleteAccessReviewCampaign", 1),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.False(owner.IsLaunched);
    }

    [Fact]
    public void ShouldRefuseSecondCompletionAndBulkAdmissionGivenCompletionPending()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var otherId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, otherId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, Uuid.CreateVersion4(), 2, NativeFacts.Ref()));

        // Act
        var completion = owner.AcceptCompletion(NativeFacts.Root(owner, otherId, "CompleteAccessReviewCampaign", 2),
            NativeFacts.Ref(), NativeFacts.Ref());
        var bulk = owner.AdmitBulkDecision(NativeFacts.Root(owner, otherId, "RecordBulkAccessDecision", 2),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, completion.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, bulk.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCompletionAndBulkAdmissionGivenCompletedCampaign()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var laterId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, laterId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent),
            CompletedFact(tenantId, campaignId, completionId, 3, intent));

        // Act
        var completion = owner.AcceptCompletion(NativeFacts.Root(owner, laterId, "CompleteAccessReviewCampaign", 3),
            NativeFacts.Ref(), NativeFacts.Ref());
        var bulk = owner.AdmitBulkDecision(NativeFacts.Root(owner, laterId, "RecordBulkAccessDecision", 3),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, completion.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, bulk.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCompletionAndBulkAdmissionGivenOperationWithoutExpectedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, commandId, LaunchFact(tenantId, campaignId));

        // Act
        var completion = owner.AcceptCompletion(NativeFacts.Root(owner, commandId, "CompleteAccessReviewCampaign", null),
            NativeFacts.Ref(), NativeFacts.Ref());
        var bulk = owner.AdmitBulkDecision(NativeFacts.Root(owner, commandId, "RecordBulkAccessDecision", null),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, completion.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, bulk.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAdmitBulkDecisionAndAdvanceRevisionGivenLaunchedCampaign()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var bulkId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, bulkId, LaunchFact(tenantId, campaignId));

        // Act
        var result = owner.AdmitBulkDecision(NativeFacts.Root(owner, bulkId, "RecordBulkAccessDecision", 1),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var admitted = Assert.IsType<AccessReviewDecisionBatchAdmitted>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal(2, admitted.Revision);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
        Assert.Equal(admitted.Revision, owner.AdmittedFor(bulkId)?.Revision);
    }

    [Fact]
    public void ShouldRefuseBulkAdmissionGivenStaleExpectedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var bulkId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, bulkId, LaunchFact(tenantId, campaignId));

        // Act
        var result = owner.AdmitBulkDecision(NativeFacts.Root(owner, bulkId, "RecordBulkAccessDecision", 0),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseBulkAdmissionGivenCampaignNeverLaunched()
    {
        // Arrange
        var bulkId = Uuid.CreateVersion4();
        var owner = new AccessReviewCampaign(Uuid.CreateVersion4(), Uuid.CreateVersion4(), bulkId);

        // Act
        var result = owner.AdmitBulkDecision(NativeFacts.Root(owner, bulkId, "RecordBulkAccessDecision", 1),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.False(owner.IsLaunched);
    }

    [Fact]
    public void ShouldCloseCampaignGivenExactPendingCompletion()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var closureId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, closureId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent));

        // Act
        var result = owner.Close(Op(tenantId, campaignId, closureId, completionId, "CompleteAccessReviewCampaignClosure"),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var completed = Assert.IsType<AccessReviewCampaignCompleted>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal(3, completed.Revision);
        Assert.Equal(intent, completed.Intent);
        Assert.Equal(campaignId, completed.CampaignId);
        Assert.True(owner.IsCompleted);
        Assert.Null(owner.Pending);
        Assert.Equal(AccessReviewCampaign.Completed, owner.Status);
    }

    [Fact]
    public void ShouldRefuseClosureGivenNoPendingCompletion()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var closureId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, closureId, LaunchFact(tenantId, campaignId));

        // Act
        var result = owner.Close(Op(tenantId, campaignId, closureId, Uuid.CreateVersion4(),
            "CompleteAccessReviewCampaignClosure"), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.False(owner.IsCompleted);
    }

    [Fact]
    public void ShouldRefuseClosureAndCancellationGivenAnotherCompletionRoot()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var closureId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, closureId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, Uuid.CreateVersion4(), 2, NativeFacts.Ref()));
        var foreign = Op(tenantId, campaignId, closureId, Uuid.CreateVersion4(), "CompleteAccessReviewCampaignClosure");

        // Act
        var closure = owner.Close(foreign, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var cancellation = owner.CancelCompletion(foreign, NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, closure.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, cancellation.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.NotNull(owner.Pending);
    }

    [Fact]
    public void ShouldCancelPendingCompletionAndReopenAdmissionGivenFailureProof()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var abortId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var proof = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, abortId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent));

        // Act
        var result = owner.CancelCompletion(Op(tenantId, campaignId, abortId, completionId,
            "AbortAccessReviewCampaignCompletion"), proof);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var canceled = Assert.IsType<AccessReviewCampaignCompletionCanceled>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal(3, canceled.Revision);
        Assert.Equal(intent, canceled.Intent);
        Assert.Equal(proof, canceled.SourceProofs);
        Assert.Null(owner.Pending);
        Assert.Equal(AccessReviewCampaign.Active, owner.Status);
        Assert.True(owner.PrepareCompletion(3).IsSuccess);
        Assert.True(owner.PrepareBulkAdmission(3).IsSuccess);
    }

    [Fact]
    public void ShouldFenceDelayedClosureOfCanceledCompletionGivenNewCompletionPending()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var closureId = Uuid.CreateVersion4();
        var firstIntent = NativeFacts.Ref();
        var secondIntent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, closureId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, firstId, 2, firstIntent),
            CanceledFact(tenantId, campaignId, firstId, 3, firstIntent),
            AcceptedFact(tenantId, campaignId, secondId, 4, secondIntent));

        // Act
        var delayed = owner.Close(Op(tenantId, campaignId, closureId, firstId, "CompleteAccessReviewCampaignClosure"),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var current = owner.Close(Op(tenantId, campaignId, closureId, secondId, "CompleteAccessReviewCampaignClosure"),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, delayed.Error?.Kind);
        Assert.True(current.IsSuccess, current.Error?.Message);
        var completed = Assert.IsType<AccessReviewCampaignCompleted>(
            Assert.Single(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents));
        Assert.Equal(5, completed.Revision);
        Assert.Equal(secondIntent, completed.Intent);
        Assert.Equal(secondId, completed.Operation.RootOperationId);
    }

    [Fact]
    public void ShouldRefuseCancellationAndClosureGivenCompletionAlreadyCanceled()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var abortId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, abortId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent),
            CanceledFact(tenantId, campaignId, completionId, 3, intent));
        var late = Op(tenantId, campaignId, abortId, completionId, "AbortAccessReviewCampaignCompletion");

        // Act
        var cancellation = owner.CancelCompletion(late, NativeFacts.Ref());
        var closure = owner.Close(late, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, cancellation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, closure.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.False(owner.IsCompleted);
    }

    [Fact]
    public void ShouldRefuseCancellationGivenCampaignAlreadyCompleted()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var abortId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, abortId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent),
            CompletedFact(tenantId, campaignId, completionId, 3, intent));

        // Act
        var result = owner.CancelCompletion(Op(tenantId, campaignId, abortId, completionId,
            "AbortAccessReviewCampaignCompletion"), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.True(owner.IsCompleted);
    }

    [Fact]
    public void ShouldReturnCommittedCompletionWithoutNewFactGivenExactRetryAfterCancellation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var intent = NativeFacts.Ref();
        var owner = Hydrate(tenantId, campaignId, completionId, LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, intent),
            CanceledFact(tenantId, campaignId, completionId, 3, intent));

        // Act
        var committed = owner.AcceptedFor(completionId);
        var canceled = owner.CanceledFor(completionId);
        var retry = owner.AcceptCompletion(NativeFacts.Root(owner, completionId, "CompleteAccessReviewCampaign", 3),
            intent, NativeFacts.Ref());

        // Assert
        Assert.NotNull(committed);
        Assert.Equal(2, committed.Revision);
        Assert.NotNull(canceled);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldReturnCommittedBulkAdmissionWithoutNewFactGivenExactRetryAfterLaterAdmission()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var firstId = Uuid.CreateVersion4();
        var owner = Hydrate(tenantId, campaignId, firstId, LaunchFact(tenantId, campaignId),
            BulkFact(tenantId, campaignId, firstId, 2), BulkFact(tenantId, campaignId, Uuid.CreateVersion4(), 3));

        // Act
        var committed = owner.AdmittedFor(firstId);
        var retry = owner.AdmitBulkDecision(NativeFacts.Root(owner, firstId, "RecordBulkAccessDecision", 3),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.NotNull(committed);
        Assert.Equal(2, committed.Revision);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldSelectNothingGivenHistoryHydratedForAnotherOperation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();

        // Act
        var owner = Hydrate(tenantId, campaignId, Uuid.CreateVersion4(), LaunchFact(tenantId, campaignId),
            AcceptedFact(tenantId, campaignId, completionId, 2, NativeFacts.Ref()));

        // Assert
        Assert.Null(owner.Acceptance.SelectedFact);
        Assert.NotNull(owner.Pending);
        Assert.Equal(owner.Pending.Revision, owner.AcceptedFor(completionId)?.Revision);
        Assert.Null(owner.AcceptedFor(Uuid.CreateVersion4()));
        Assert.Null(owner.CanceledFor(completionId));
    }

    [Fact]
    public void ShouldAppendNothingGivenCompletionOnReplayedCampaignWithoutFinalizedCut()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var completionId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessReviewCampaign(tenantId, campaignId, completionId),
            [LaunchFact(tenantId, campaignId)]);

        // Act
        var result = owner.AcceptCompletion(NativeFacts.Root(owner, completionId, "CompleteAccessReviewCampaign", 1),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    static AccessReviewCampaign Hydrate(Uuid tenantId, Uuid campaignId, Uuid selectedLogicalId,
        params DomainEvent[] facts)
    {
        var owner = NativeFacts.Replay(new AccessReviewCampaign(tenantId, campaignId, selectedLogicalId), facts);
        owner.ReplayCut.Confirm(owner, owner.ReplayCut.Digest);
        return owner;
    }

    static EventStreamAddress StreamOf(Uuid tenantId, Uuid campaignId) =>
        new(tenantId.ToString(), AccessReviewCampaign.Area, campaignId.ToString());

    static OperationProvenance Op(Uuid tenantId, Uuid campaignId, Uuid logicalId, Uuid rootId, string kind,
        ulong? expectedRevision = null) => new(logicalId, rootId, null, kind, Actor, StreamOf(tenantId, campaignId),
        "initial", expectedRevision, new string('b', 64), NativeFacts.Now, StreamOf(tenantId, campaignId),
        StreamOf(tenantId, campaignId), StreamOf(tenantId, campaignId));

    static Result Launch(AccessReviewCampaign owner, Uuid logicalId) => owner.Launch(
        NativeFacts.Root(owner, logicalId, "LaunchAccessReviewCampaign", null), " Q3 review ", " Review access. ",
        NativeFacts.Now.AddDays(14), Uuid.CreateVersion4(), new string('c', 64), NativeFacts.Ref(),
        NativeFacts.Ref(), NativeFacts.Ref(), Actor, NativeFacts.Now, null, null, NativeFacts.Ref(),
        NativeFacts.Ref());

    static DomainEvent LaunchFact(Uuid tenantId, Uuid campaignId)
    {
        var author = new AccessReviewCampaign(tenantId, campaignId, campaignId);
        var launched = Launch(author, campaignId);
        Assert.True(launched.IsSuccess, launched.Error?.Message);
        return NativeFacts.Raised(author).Single();
    }

    static AccessReviewCampaignCompletionAccepted AcceptedFact(Uuid tenantId, Uuid campaignId, Uuid completionId, long revision,
        ImmutableContentRef intent) => DomainEventSeed.Attach(new AccessReviewCampaignCompletionAccepted(
        StreamOf(tenantId, campaignId), revision, 1, intent, null,
        Op(tenantId, campaignId, completionId, completionId, "CompleteAccessReviewCampaign", (ulong)(revision - 1)),
        NativeFacts.Ref(), null), campaignId, (ulong)revision);

    static AccessReviewDecisionBatchAdmitted BulkFact(Uuid tenantId, Uuid campaignId, Uuid bulkId, long revision) =>
        DomainEventSeed.Attach(new AccessReviewDecisionBatchAdmitted(StreamOf(tenantId, campaignId), revision, 1,
            NativeFacts.Ref(), null,
            Op(tenantId, campaignId, bulkId, bulkId, "RecordBulkAccessDecision", (ulong)(revision - 1)),
            NativeFacts.Ref(), null), campaignId, (ulong)revision);

    static AccessReviewCampaignCompleted CompletedFact(Uuid tenantId, Uuid campaignId, Uuid completionId, long revision,
        ImmutableContentRef intent) => DomainEventSeed.Attach(new AccessReviewCampaignCompleted(tenantId, campaignId,
        revision, NativeFacts.Ref(),
        Op(tenantId, campaignId, Uuid.CreateVersion4(), completionId, "CompleteAccessReviewCampaignClosure"), 1,
        NativeFacts.Ref(), intent, null, NativeFacts.Ref()), campaignId, (ulong)revision);

    static AccessReviewCampaignCompletionCanceled CanceledFact(Uuid tenantId, Uuid campaignId, Uuid completionId, long revision,
        ImmutableContentRef intent) => DomainEventSeed.Attach(new AccessReviewCampaignCompletionCanceled(
        StreamOf(tenantId, campaignId), revision, 1, intent, NativeFacts.Ref(),
        Op(tenantId, campaignId, Uuid.CreateVersion4(), completionId, "AbortAccessReviewCampaignCompletion"), null,
        null), campaignId, (ulong)revision);
}
