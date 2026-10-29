using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>Owner-level tests of one frozen review item: identity, materialization, guards and valid replay.</summary>
public sealed class AccessReviewItemTests
{
    [Fact]
    public void ShouldDeriveOneStreamGivenSameTenantCampaignAndItem()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();

        // Act
        var first = new AccessReviewItem(tenantId, campaignId, itemId);
        var second = new AccessReviewItem(tenantId, campaignId, itemId);
        var otherItem = new AccessReviewItem(tenantId, campaignId, Uuid.CreateVersion4());
        var otherCampaign = new AccessReviewItem(tenantId, Uuid.CreateVersion4(), itemId);

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Stream, second.Stream);
        Assert.NotEqual(first.Id, otherItem.Id);
        Assert.NotEqual(first.Id, otherCampaign.Id);
    }

    [Fact]
    public void ShouldRaiseRevisionOneOpenedFactGivenNewItem()
    {
        // Arrange
        var owner = Fresh(out var tenantId, out var campaignId, out var itemId);

        // Act
        var result = owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), "MaterializeAccessReviewItem", null),
            NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.IsType<AccessReviewItemOpened>(
            Assert.Single(new AggregateScenario<AccessReviewItem>(owner).PendingEvents));
        Assert.Equal(1, raised.Revision);
        Assert.Equal(tenantId, raised.TenantId);
        Assert.Equal(campaignId, raised.CampaignId);
        Assert.Equal(itemId, raised.ItemId);
        Assert.Equal(owner.Stream, raised.Owner);
        Assert.Equal(1, owner.Revision);
        Assert.Same(raised, owner.Opened);
        Assert.Same(raised, owner.Latest);
        Assert.False(owner.IsClosed);
    }

    [Fact]
    public void ShouldAppendNothingGivenMaterializationOnAnotherStream()
    {
        // Arrange
        var owner = Fresh(out _, out _, out _);
        var foreign = Fresh(out _, out _, out _);

        // Act
        var result = owner.Materialize(NativeFacts.Root(foreign, Uuid.CreateVersion4(), "MaterializeAccessReviewItem", null),
            NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.Opened);
    }

    [Fact]
    public void ShouldAppendNothingGivenChangeBeforeMaterialization()
    {
        // Arrange
        var owner = Fresh(out _, out _, out _);

        // Act
        var decision = owner.RecordDecision(NativeFacts.Root(owner, Uuid.CreateVersion4(), "RecordAccessDecisions", 0),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var reassignment = owner.Reassign(NativeFacts.Root(owner, Uuid.CreateVersion4(), "ReassignAccessReviewResponsibility", 0),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, decision.Error?.Kind);
        Assert.True(decision.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, reassignment.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenChangeWithoutExpectedRevisionOrOwnStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId),
            History(tenantId, campaignId, itemId, 1));
        var foreign = Fresh(out _, out _, out _);

        // Act
        var withoutRevision = owner.Verify(NativeFacts.Root(owner, Uuid.CreateVersion4(), "VerifyAccessRemediation", null),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var foreignOperation = owner.RecordException(
            NativeFacts.Root(foreign, Uuid.CreateVersion4(), "ExemptAccessRemediation", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, foreignOperation.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreItemStateGivenValidOpenedDecisionAndChangeHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var facts = History(tenantId, campaignId, itemId, 3);

        // Act
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId), facts);

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.NotNull(owner.Opened);
        Assert.IsType<AccessRemediationChangeRecorded>(owner.Latest);
        Assert.Equal(tenantId, owner.TenantId);
        Assert.Equal(campaignId, owner.CampaignId);
        Assert.Equal(itemId, owner.ItemId);
        Assert.False(owner.IsClosed);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAcceptChangePreparationOnlyGivenCurrentRevisionOfReplayedItem()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId),
            History(tenantId, campaignId, itemId, 2));

        // Act
        var current = owner.PrepareChange(2);
        var stale = owner.PrepareChange(1);
        var ahead = owner.PrepareChange(3);

        // Assert
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, ahead.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenOpenedItem()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId),
            History(tenantId, campaignId, itemId, 1));

        // Act
        var result = owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), "MaterializeAccessReviewItem", null),
            NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenDecisionOnReplayedItemWithoutFinalizedCut()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId),
            History(tenantId, campaignId, itemId, 1));

        // Act
        var result = owner.RecordDecision(NativeFacts.Root(owner, Uuid.CreateVersion4(), "RecordAccessDecisions", 1),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseBulkHoldGivenUnmaterializedOrForeignBatchItem()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var unmaterialized = new AccessReviewItem(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            selectedBatchId: batchId);
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var opened = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId, selectedBatchId: batchId),
            History(tenantId, campaignId, itemId, 1));

        // Act
        var notYet = unmaterialized.PrepareBulkHold(batchId);
        var foreign = opened.PrepareBulkHold(Uuid.CreateVersion4());
        var own = opened.PrepareBulkHold(batchId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, notYet.Error?.Kind);
        Assert.True(notYet.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Validation, foreign.Error?.Kind);
        Assert.True(own.IsSuccess, own.Error?.Message);
    }

    [Fact]
    public void ShouldRefuseFenceGivenUnmaterializedItemOrForeignBatchRoot()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = new AccessReviewItem(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            selectedBatchId: batchId);

        // Act
        var unmaterialized = owner.FenceBulkDecision(NativeFacts.Root(owner, batchId, "CancelAccessReviewDecisionBatch", null),
            NativeFacts.Ref(), 1);
        var foreign = owner.FenceBulkDecision(
            NativeFacts.Root(owner, Uuid.CreateVersion4(), "CancelAccessReviewDecisionBatch", null), NativeFacts.Ref(), 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, unmaterialized.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, foreign.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSecondFenceAndHoldGivenBatchAlreadyFencedOnItem()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var author = new AccessReviewItem(tenantId, campaignId, itemId, selectedBatchId: batchId);
        var facts = new List<DomainEvent>(History(tenantId, campaignId, itemId, 1))
        {
            DomainEventSeed.Attach(new AccessReviewItemDecisionCanceled(tenantId, campaignId, itemId, author.Stream, 1, 1,
                NativeFacts.Ref(), null, NativeFacts.Root(author, batchId, "CancelAccessReviewDecisionBatch", null), null,
                null), author.Id, 2)
        };
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId, selectedBatchId: batchId), facts);

        // Act
        var fenced = owner.IsFenced(batchId);
        var revision = owner.Revision;
        var second = owner.FenceBulkDecision(NativeFacts.Root(owner, batchId, "CancelAccessReviewDecisionBatch", null),
            NativeFacts.Ref(), 1);
        var hold = owner.PrepareBulkHold(batchId);

        // Assert
        Assert.True(fenced);
        Assert.Equal(1, revision);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, hold.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseFenceGivenBatchAlreadyFencedByAnotherOperationUnderConfirmedCut()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var secondLogicalId = Uuid.CreateVersion4();
        var author = new AccessReviewItem(tenantId, campaignId, itemId);
        var firstFence = NativeFacts.Root(author, Uuid.CreateVersion4(), "CancelAccessReviewDecisionBatch", null) with
        {
            RootOperationId = batchId
        };
        var owner = Confirmed(tenantId, campaignId, itemId, secondLogicalId, batchId,
            [
                .. History(tenantId, campaignId, itemId, 1),
                DomainEventSeed.Attach(new AccessReviewItemDecisionCanceled(tenantId, campaignId, itemId, author.Stream, 1,
                    1, NativeFacts.Ref(), null, firstFence, null, null), author.Id, 2)
            ]);
        var secondFence = NativeFacts.Root(owner, secondLogicalId, "CancelAccessReviewDecisionBatch", null) with
        {
            RootOperationId = batchId
        };

        // Act
        var result = owner.FenceBulkDecision(secondFence, NativeFacts.Ref(), 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNextRevisionDecisionGivenConfirmedCutAndCurrentRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var owner = Confirmed(tenantId, campaignId, itemId, logicalId, default, History(tenantId, campaignId, itemId, 1));

        // Act
        var result = owner.RecordDecision(NativeFacts.Root(owner, logicalId, "RecordAccessDecisions", 1),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.IsType<AccessDecisionsRecorded>(
            Assert.Single(new AggregateScenario<AccessReviewItem>(owner).PendingEvents));
        Assert.Equal(2, raised.Revision);
        Assert.Null(raised.DecisionBatchId);
        Assert.False(raised.PublicationRequired);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondChangeGivenStaleExpectedRevisionAfterFirstUnderConfirmedCut()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var owner = Confirmed(tenantId, campaignId, itemId, logicalId, default, History(tenantId, campaignId, itemId, 1));
        var first = owner.RecordDecision(NativeFacts.Root(owner, logicalId, "RecordAccessDecisions", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Act
        var stale = owner.RecordDecision(NativeFacts.Root(owner, logicalId, "RecordAccessDecisions", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Single(new AggregateScenario<AccessReviewItem>(owner).PendingEvents);
        Assert.Equal(2, owner.Revision);
    }

    static AccessReviewItem Confirmed(Uuid tenantId, Uuid campaignId, Uuid itemId, Uuid logicalId, Uuid batchId,
        DomainEvent[] facts)
    {
        var owner = NativeFacts.Replay(new AccessReviewItem(tenantId, campaignId, itemId, logicalId, batchId), facts);
        owner.ReplayCut.Confirm(owner, owner.ReplayCut.Digest);
        return owner;
    }

    static AccessReviewItem Fresh(out Uuid tenantId, out Uuid campaignId, out Uuid itemId)
    {
        tenantId = Uuid.CreateVersion4();
        campaignId = Uuid.CreateVersion4();
        itemId = Uuid.CreateVersion4();
        return new AccessReviewItem(tenantId, campaignId, itemId);
    }

    /// <summary>Opened (revision 1), then a single decision (2) and a provider change (3), as committed facts.</summary>
    static DomainEvent[] History(Uuid tenantId, Uuid campaignId, Uuid itemId, int count)
    {
        var author = new AccessReviewItem(tenantId, campaignId, itemId);
        var opened = author.Materialize(NativeFacts.Root(author, Uuid.CreateVersion4(), "MaterializeAccessReviewItem", null),
            NativeFacts.Ref());
        Assert.True(opened.IsSuccess, opened.Error?.Message);
        var facts = new List<DomainEvent>(NativeFacts.Raised(author))
        {
            DomainEventSeed.Attach(new AccessDecisionsRecorded(tenantId, campaignId, itemId, 2, NativeFacts.Ref(),
                NativeFacts.Root(author, Uuid.CreateVersion4(), "RecordAccessDecisions", 1), null, false,
                NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref()), author.Id, 2),
            DomainEventSeed.Attach(new AccessRemediationChangeRecorded(tenantId, campaignId, 3, itemId, NativeFacts.Ref(),
                NativeFacts.Root(author, Uuid.CreateVersion4(), "RecordAccessRemediationChange", 2), NativeFacts.Ref(),
                NativeFacts.Ref()), author.Id, 3)
        };
        return facts.Take(count).ToArray();
    }
}
