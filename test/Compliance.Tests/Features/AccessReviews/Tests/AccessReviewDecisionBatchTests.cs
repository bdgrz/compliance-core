using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>Owner-level tests of the decision batch: materialize, publish or cancel exactly once, and valid replay.</summary>
public sealed class AccessReviewDecisionBatchTests
{
    [Fact]
    public void ShouldMaterializeAtRevisionZeroGivenFreshBatch()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Open(batchId);
        var intent = NativeFacts.Ref();

        // Act
        var result = owner.Materialize(NativeFacts.Root(owner, batchId, "MaterializeAccessReviewDecisionBatch", null), intent);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.IsType<AccessReviewDecisionBatchAccepted>(
            Assert.Single(new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents));
        Assert.Equal(0, raised.Revision);
        Assert.Equal(owner.Stream, raised.Owner);
        Assert.Equal(intent, raised.Intent);
        Assert.Equal(0, owner.Revision);
        Assert.True(owner.IsMaterialized);
        Assert.Equal(batchId, owner.DecisionBatchId);
    }

    [Fact]
    public void ShouldDeriveOneStreamGivenSameTenantCampaignAndBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();

        // Act
        var first = new AccessReviewDecisionBatch(tenantId, campaignId, batchId, Uuid.CreateVersion4());
        var second = new AccessReviewDecisionBatch(tenantId, campaignId, batchId);
        var otherBatch = new AccessReviewDecisionBatch(tenantId, campaignId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Stream, second.Stream);
        Assert.NotEqual(first.Stream, otherBatch.Stream);
    }

    [Fact]
    public void ShouldAppendNothingGivenMaterializationOnForeignStreamOrRootedElsewhere()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Open(batchId);

        // Act
        var foreignStream = owner.Materialize(
            NativeFacts.Root(Open(Uuid.CreateVersion4()), batchId, "MaterializeAccessReviewDecisionBatch", null),
            NativeFacts.Ref());
        var rootedElsewhere = owner.Materialize(
            NativeFacts.Root(owner, Uuid.CreateVersion4(), "MaterializeAccessReviewDecisionBatch", null), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreignStream.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, rootedElsewhere.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents);
        Assert.False(owner.IsMaterialized);
    }

    [Fact]
    public void ShouldAppendNothingGivenSecondMaterialization()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Materialized(batchId);

        // Act
        var result = owner.Materialize(NativeFacts.Root(owner, batchId, "MaterializeAccessReviewDecisionBatch", null),
            NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldPublishAtRevisionOneGivenMaterializedBatch()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Materialized(batchId);
        var proofs = NativeFacts.Ref();
        var result = NativeFacts.Ref();

        // Act
        var published = owner.Publish(Child(owner), proofs, result);

        // Assert
        Assert.True(published.IsSuccess, published.Error?.Message);
        var raised = Assert.IsType<AccessReviewDecisionBatchPublished>(
            new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents[1]);
        Assert.Equal(1, raised.Revision);
        Assert.Equal(owner.Accepted!.Intent, raised.Intent);
        Assert.Equal(proofs, raised.SourceProofs);
        Assert.Equal(result, raised.OperationResult);
        Assert.Equal(1, owner.Revision);
        Assert.NotNull(owner.Published);
    }

    [Fact]
    public void ShouldAppendNothingGivenPublicationBeforeMaterialization()
    {
        // Arrange
        var owner = Open(Uuid.CreateVersion4());

        // Act
        var result = owner.Publish(Child(owner), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenSecondPublicationOrPublicationAfterCancellation()
    {
        // Arrange
        var published = Materialized(Uuid.CreateVersion4());
        published.Publish(Child(published), NativeFacts.Ref(), NativeFacts.Ref());
        var canceling = Materialized(Uuid.CreateVersion4());
        canceling.AcceptCancellation(Child(canceling), NativeFacts.Ref());

        // Act
        var again = published.Publish(Child(published), NativeFacts.Ref(), NativeFacts.Ref());
        var afterCancellation = canceling.Publish(Child(canceling), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, afterCancellation.Error?.Kind);
        Assert.Equal(2, new AggregateScenario<AccessReviewDecisionBatch>(published).PendingEvents.Count);
        Assert.Equal(2, new AggregateScenario<AccessReviewDecisionBatch>(canceling).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAcceptAndCompleteCancellationGivenMaterializedUnpublishedBatch()
    {
        // Arrange
        var owner = Materialized(Uuid.CreateVersion4());
        var failureProof = NativeFacts.Ref();
        var fences = NativeFacts.Ref();

        // Act
        var accepted = owner.AcceptCancellation(Child(owner), failureProof);
        var revisionAfterAcceptance = owner.Revision;
        var completed = owner.CompleteCancellation(Child(owner), fences);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal(1, revisionAfterAcceptance);
        Assert.Equal(2, owner.Revision);
        var pending = new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents;
        Assert.Equal(1, Assert.IsType<AccessReviewDecisionBatchCancellationAccepted>(pending[1]).Revision);
        Assert.Equal(failureProof, Assert.IsType<AccessReviewDecisionBatchCancellationAccepted>(pending[1]).SourceProofs);
        Assert.Equal(2, Assert.IsType<AccessReviewDecisionBatchCanceled>(pending[2]).Revision);
        Assert.Equal(fences, Assert.IsType<AccessReviewDecisionBatchCanceled>(pending[2]).SourceProofs);
        Assert.Null(owner.Published);
    }

    [Fact]
    public void ShouldAppendNothingGivenCancellationBeforeMaterializationAfterPublicationOrRepeated()
    {
        // Arrange
        var fresh = Open(Uuid.CreateVersion4());
        var published = Materialized(Uuid.CreateVersion4());
        published.Publish(Child(published), NativeFacts.Ref(), NativeFacts.Ref());
        var canceling = Materialized(Uuid.CreateVersion4());
        canceling.AcceptCancellation(Child(canceling), NativeFacts.Ref());

        // Act
        var beforeMaterialization = fresh.AcceptCancellation(Child(fresh), NativeFacts.Ref());
        var afterPublication = published.AcceptCancellation(Child(published), NativeFacts.Ref());
        var repeated = canceling.AcceptCancellation(Child(canceling), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, beforeMaterialization.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, afterPublication.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, repeated.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewDecisionBatch>(fresh).PendingEvents);
        Assert.Equal(2, new AggregateScenario<AccessReviewDecisionBatch>(published).PendingEvents.Count);
        Assert.Equal(2, new AggregateScenario<AccessReviewDecisionBatch>(canceling).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAppendNothingGivenCompletionWithoutAcceptedCancellationOrRepeated()
    {
        // Arrange
        var unpublished = Materialized(Uuid.CreateVersion4());
        var completedOwner = Materialized(Uuid.CreateVersion4());
        completedOwner.AcceptCancellation(Child(completedOwner), NativeFacts.Ref());
        completedOwner.CompleteCancellation(Child(completedOwner), NativeFacts.Ref());

        // Act
        var withoutAcceptance = unpublished.CompleteCancellation(Child(unpublished), NativeFacts.Ref());
        var repeated = completedOwner.CompleteCancellation(Child(completedOwner), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, withoutAcceptance.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, repeated.Error?.Kind);
        Assert.Single(new AggregateScenario<AccessReviewDecisionBatch>(unpublished).PendingEvents);
        Assert.Equal(3, new AggregateScenario<AccessReviewDecisionBatch>(completedOwner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAppendNothingGivenFactOnForeignStream()
    {
        // Arrange
        var owner = Materialized(Uuid.CreateVersion4());
        var foreign = Child(Open(Uuid.CreateVersion4()));

        // Act
        var publish = owner.Publish(foreign, NativeFacts.Ref(), NativeFacts.Ref());
        var cancel = owner.AcceptCancellation(foreign, NativeFacts.Ref());
        var complete = owner.CompleteCancellation(foreign, NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, publish.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, cancel.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, complete.Error?.Kind);
        Assert.Single(new AggregateScenario<AccessReviewDecisionBatch>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreStateGivenValidPublishedHistory()
    {
        // Arrange
        var (tenantId, campaignId, batchId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var source = new AccessReviewDecisionBatch(tenantId, campaignId, batchId);
        source.Materialize(NativeFacts.Root(source, batchId, "MaterializeAccessReviewDecisionBatch", null), NativeFacts.Ref());
        source.Publish(Child(source), NativeFacts.Ref(), NativeFacts.Ref());
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(new AccessReviewDecisionBatch(tenantId, campaignId, batchId), facts);

        // Assert
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsMaterialized);
        Assert.NotNull(owner.Published);
        Assert.Null(owner.CancellationAccepted);
        Assert.Equal(2, owner.Facts.Count);
    }

    [Fact]
    public void ShouldRestoreStateGivenValidCanceledHistory()
    {
        // Arrange
        var (tenantId, campaignId, batchId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var source = new AccessReviewDecisionBatch(tenantId, campaignId, batchId);
        source.Materialize(NativeFacts.Root(source, batchId, "MaterializeAccessReviewDecisionBatch", null), NativeFacts.Ref());
        source.AcceptCancellation(Child(source), NativeFacts.Ref());
        source.CompleteCancellation(Child(source), NativeFacts.Ref());
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(new AccessReviewDecisionBatch(tenantId, campaignId, batchId), facts);
        var publish = owner.Publish(Child(owner), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, publish.Error?.Kind);
        Assert.Equal(2, owner.Revision);
        Assert.NotNull(owner.Canceled);
        Assert.Null(owner.Published);
    }

    [Fact]
    public void ShouldReplayOriginalMaterializationGivenExactRetryAfterPublication()
    {
        // Arrange
        var (tenantId, campaignId, batchId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var commandId = Uuid.CreateVersion4();
        var source = new AccessReviewDecisionBatch(tenantId, campaignId, batchId);
        var provenance = NativeFacts.Root(source, commandId, "MaterializeAccessReviewDecisionBatch", null);
        source.Materialize(provenance with { RootOperationId = batchId }, NativeFacts.Ref());
        source.Publish(Child(source), NativeFacts.Ref(), NativeFacts.Ref());
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(new AccessReviewDecisionBatch(tenantId, campaignId, batchId, commandId), facts);
        var exact = owner.Acceptance.Decide(AcceptedOperationBinding.Of(provenance with { RootOperationId = batchId }));
        var changed = owner.Acceptance.Decide(AcceptedOperationBinding.Of(provenance with
        {
            AuthoredIntentSha256 = new string('c', 64)
        }));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.IsType<AccessReviewDecisionBatchAccepted>(owner.Acceptance.SelectedFact);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldSelectNothingGivenUnrelatedLogicalIdOnHydration()
    {
        // Arrange
        var (tenantId, campaignId, batchId) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var source = new AccessReviewDecisionBatch(tenantId, campaignId, batchId);
        source.Materialize(NativeFacts.Root(source, batchId, "MaterializeAccessReviewDecisionBatch", null), NativeFacts.Ref());
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(
            new AccessReviewDecisionBatch(tenantId, campaignId, batchId, Uuid.CreateVersion4()), facts);

        // Assert
        Assert.Null(owner.Acceptance.SelectedFact);
        Assert.True(owner.IsMaterialized);
    }

    static AccessReviewDecisionBatch Open(Uuid batchId) =>
        new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), batchId);

    static AccessReviewDecisionBatch Materialized(Uuid batchId)
    {
        var owner = Open(batchId);
        var result = owner.Materialize(NativeFacts.Root(owner, batchId, "MaterializeAccessReviewDecisionBatch", null),
            NativeFacts.Ref());
        Assert.True(result.IsSuccess, result.Error?.Message);
        return owner;
    }

    static OperationProvenance Child(AccessReviewDecisionBatch owner) =>
        NativeFacts.Root(owner, Uuid.CreateVersion4(), "BatchStep", null);
}
