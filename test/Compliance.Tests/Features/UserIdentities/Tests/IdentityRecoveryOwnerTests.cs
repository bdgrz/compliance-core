using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>Owner-level decisions of one identity recovery challenge, with no host, handler or process.</summary>
public sealed class IdentityRecoveryOwnerTests
{
    [Fact]
    public void ShouldFreezeCandidateAtChallengeStreamGivenFirstMaterialize()
    {
        // Arrange
        var challenge = Uuid.CreateVersion4();
        var owner = new IdentityRecovery(challenge);
        var intent = NativeFacts.Ref();
        var sourceProofs = NativeFacts.Ref();

        // Act
        var result = owner.Materialize(Operation(owner, "MaterializeIdentityRecoveryPreparation"), intent, sourceProofs);
        var facts = Facts(owner);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(IdentityRecovery.Stage.Prepared, owner.State);
        Assert.Equal(intent, owner.Intent);
        var prepared = Assert.IsType<IdentityRecoveryPrepared>(Assert.Single(facts));
        Assert.Equal(challenge, prepared.ChallengeId);
        Assert.Equal(owner.Stream, prepared.Owner);
        Assert.Equal(challenge.ToString(), owner.Stream.Resource);
        Assert.Equal(IdentityRecovery.FrozenGeneration, prepared.Generation);
        Assert.Equal(0L, prepared.Revision);
        Assert.Equal(sourceProofs, prepared.SourceProofs);
        Assert.Equal(intent, prepared.Intent);
    }

    [Fact]
    public void ShouldKeepStreamsDistinctGivenDifferentChallenges()
    {
        // Arrange
        var first = new IdentityRecovery(Uuid.CreateVersion4());
        var second = new IdentityRecovery(Uuid.CreateVersion4());

        // Act
        var same = new IdentityRecovery(first.Id, Uuid.CreateVersion4());

        // Assert
        Assert.NotEqual(first.Stream, second.Stream);
        Assert.Equal(first.Stream, same.Stream);
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenAnyDecisionBeforePreparation()
    {
        // Arrange
        var owner = new IdentityRecovery(Uuid.CreateVersion4());
        var intent = NativeFacts.Ref();

        // Act
        var accept = owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref());
        var complete = owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), intent, NativeFacts.Ref());
        var reject = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), intent, NativeFacts.Ref());

        // Assert
        Assert.False(accept.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, accept.Error!.Kind);
        Assert.False(complete.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, complete.Error!.Kind);
        Assert.False(reject.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, reject.Error!.Kind);
        Assert.Equal(IdentityRecovery.Stage.None, owner.State);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseSecondCandidateAndAppendNothingGivenFrozenPreparation()
    {
        // Arrange
        var owner = Prepared(out var intent);

        // Act
        var same = owner.Materialize(Operation(owner, "MaterializeIdentityRecoveryPreparation"), intent, NativeFacts.Ref());
        var changed = owner.Materialize(Operation(owner, "MaterializeIdentityRecoveryPreparation"), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(same.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, same.Error!.Kind);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(intent, owner.Intent);
        Assert.Single(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseAcceptRejectAndCompleteGivenDifferentIntentOrSkippedStage()
    {
        // Arrange
        var owner = Prepared(out _);
        var other = NativeFacts.Ref();

        // Act
        var accept = owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), other, NativeFacts.Ref());
        var reject = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), other, NativeFacts.Ref());
        var complete = owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), owner.Intent!, NativeFacts.Ref());

        // Assert
        Assert.False(accept.IsSuccess);
        Assert.False(reject.IsSuccess);
        Assert.False(complete.IsSuccess);
        Assert.Equal(IdentityRecovery.Stage.Prepared, owner.State);
        Assert.Single(Facts(owner));
    }

    [Fact]
    public void ShouldCompleteForwardAtConsecutiveRevisionsGivenAcceptedSwap()
    {
        // Arrange
        var owner = Prepared(out var intent);
        var protection = NativeFacts.Ref();
        var completion = NativeFacts.Ref();

        // Act
        var accept = owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), intent, protection);
        var reject = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), intent, NativeFacts.Ref());
        var complete = owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), intent, completion);
        var facts = Facts(owner);

        // Assert
        Assert.True(accept.IsSuccess, accept.Error?.Message);
        Assert.False(reject.IsSuccess);
        Assert.True(complete.IsSuccess, complete.Error?.Message);
        Assert.Equal(IdentityRecovery.Stage.Completed, owner.State);
        Assert.Equal(3, facts.Length);
        Assert.Equal(protection, Assert.IsType<IdentityRecoveryAccepted>(facts[1]).SourceProofs);
        Assert.Equal(completion, Assert.IsType<IdentityRecoveryCompleted>(facts[2]).SourceProofs);
        var expectedRevisions = new long[] { 0, 1, 2 };
        Assert.Equal(expectedRevisions, facts.Cast<IIdentityRecoveryFact>().Select(fact => fact.Revision).ToArray());
        Assert.DoesNotContain(facts, fact => fact is IdentityRecoveryPreparationRejected);
    }

    [Fact]
    public void ShouldRefuseEveryLaterDecisionGivenCompletedSwap()
    {
        // Arrange
        var owner = Prepared(out var intent);
        Assert.True(owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), intent, NativeFacts.Ref()).IsSuccess);

        // Act
        var accept = owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref());
        var complete = owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), intent, NativeFacts.Ref());
        var reject = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), intent, NativeFacts.Ref());

        // Assert
        Assert.False(accept.IsSuccess);
        Assert.False(complete.IsSuccess);
        Assert.False(reject.IsSuccess);
        Assert.Equal(IdentityRecovery.Stage.Completed, owner.State);
        Assert.Equal(3, Facts(owner).Length);
    }

    [Fact]
    public void ShouldFenceAcceptAndCompleteGivenRejectedPreparation()
    {
        // Arrange
        var owner = Prepared(out var intent);
        var conflict = NativeFacts.Ref();

        // Act
        var reject = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), intent, conflict);
        var again = owner.Reject(Operation(owner, "RejectIdentityRecoveryPreparation"), intent, conflict);
        var accept = owner.Accept(Operation(owner, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref());
        var complete = owner.Complete(Operation(owner, "CompleteAcceptedIdentityRecovery"), intent, NativeFacts.Ref());
        var facts = Facts(owner);

        // Assert
        Assert.True(reject.IsSuccess, reject.Error?.Message);
        Assert.False(again.IsSuccess);
        Assert.False(accept.IsSuccess);
        Assert.False(complete.IsSuccess);
        Assert.Equal(IdentityRecovery.Stage.Rejected, owner.State);
        Assert.Equal(2, facts.Length);
        Assert.Equal(conflict, Assert.IsType<IdentityRecoveryPreparationRejected>(facts[1]).SourceProofs);
    }

    [Fact]
    public void ShouldHydrateEachStageGivenValidHistory()
    {
        // Arrange
        var challenge = Uuid.CreateVersion4();
        var seed = new IdentityRecovery(challenge);
        var intent = NativeFacts.Ref();
        Assert.True(seed.Materialize(Operation(seed, "MaterializeIdentityRecoveryPreparation"), intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(seed.Accept(Operation(seed, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(seed.Complete(Operation(seed, "CompleteAcceptedIdentityRecovery"), intent, NativeFacts.Ref()).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var prepared = NativeFacts.Replay(new IdentityRecovery(challenge), history.Take(1));
        var accepted = NativeFacts.Replay(new IdentityRecovery(challenge), history.Take(2));
        var completed = NativeFacts.Replay(new IdentityRecovery(challenge), history);

        // Assert
        Assert.Equal(IdentityRecovery.Stage.Prepared, prepared.State);
        Assert.Equal(IdentityRecovery.Stage.Accepted, accepted.State);
        Assert.Equal(IdentityRecovery.Stage.Completed, completed.State);
        Assert.Equal(intent, completed.Intent);
        Assert.Equal(seed.Stream, completed.Stream);
    }

    [Fact]
    public void ShouldContinueRejectionGivenReplayedPreparation()
    {
        // Arrange
        var challenge = Uuid.CreateVersion4();
        var seed = new IdentityRecovery(challenge);
        var intent = NativeFacts.Ref();
        Assert.True(seed.Materialize(Operation(seed, "MaterializeIdentityRecoveryPreparation"), intent, NativeFacts.Ref()).IsSuccess);
        var replayed = NativeFacts.Replay(new IdentityRecovery(challenge), NativeFacts.Raised(seed));

        // Act
        var reject = replayed.Reject(Operation(replayed, "RejectIdentityRecoveryPreparation"), intent, NativeFacts.Ref());
        var accept = replayed.Accept(Operation(replayed, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref());

        // Assert
        Assert.True(reject.IsSuccess, reject.Error?.Message);
        Assert.False(accept.IsSuccess);
        Assert.Equal(IdentityRecovery.Stage.Rejected, replayed.State);
    }

    [Fact]
    public void ShouldFindCommittedOperationAndConflictChangedIntentGivenReplayAfterLaterFacts()
    {
        // Arrange
        var challenge = Uuid.CreateVersion4();
        var seed = new IdentityRecovery(challenge);
        var intent = NativeFacts.Ref();
        var materialize = Operation(seed, "MaterializeIdentityRecoveryPreparation");
        Assert.True(seed.Materialize(materialize, intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(seed.Accept(Operation(seed, "AcceptProtectedIdentityRecovery"), intent, NativeFacts.Ref()).IsSuccess);
        var history = NativeFacts.Raised(seed);
        var exact = AcceptedOperationBinding.Of(materialize);
        var changed = exact with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var retry = NativeFacts.Replay(new IdentityRecovery(challenge, materialize.LogicalId), history);
        var exactDecision = retry.Acceptance.Decide(exact);
        var changedDecision = retry.Acceptance.Decide(changed);
        var unrelated = NativeFacts.Replay(new IdentityRecovery(challenge, Uuid.CreateVersion4()), history);

        // Assert
        Assert.Equal(IdentityRecovery.Stage.Accepted, retry.State);
        Assert.IsType<IdentityRecoveryPrepared>(retry.Acceptance.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exactDecision);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changedDecision);
        Assert.Null(unrelated.Acceptance.SelectedFact);
    }

    static IdentityRecovery Prepared(out ImmutableContentRef intent)
    {
        var owner = new IdentityRecovery(Uuid.CreateVersion4());
        intent = NativeFacts.Ref();
        Assert.True(owner.Materialize(Operation(owner, "MaterializeIdentityRecoveryPreparation"), intent, NativeFacts.Ref()).IsSuccess);
        return owner;
    }

    static DomainEvent[] Facts(IdentityRecovery owner) => new AggregateScenario<IdentityRecovery>(owner).PendingEvents.ToArray();

    static OperationProvenance Operation(IdentityRecovery owner, string commandKind) =>
        NativeFacts.Root(owner, Uuid.CreateVersion4(), commandKind, null);
}
