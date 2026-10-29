using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

/// <summary>The comment thread owner's own decisions: identity, once-only materialization, acceptance and replay.</summary>
public sealed class AuditorFeedbackThreadTests
{
    const string CommandKind = "MaterializeAuditorComment";

    [Fact]
    public void ShouldDeriveStableIdentityGivenSameTenantProgramRequestAndThread()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();

        // Act
        var first = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);
        var again = new AuditorFeedbackThread(tenantId, programId, requestId, threadId, Uuid.CreateVersion4());
        var otherThread = new AuditorFeedbackThread(tenantId, programId, requestId, Uuid.CreateVersion4());
        var otherRequest = new AuditorFeedbackThread(tenantId, programId, Uuid.CreateVersion4(), threadId);
        var otherTenant = new AuditorFeedbackThread(Uuid.CreateVersion4(), programId, requestId, threadId);

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Stream, again.Stream);
        Assert.Equal(AuditorFeedbackThread.IdentityOf(tenantId, programId, requestId, threadId), first.Id);
        Assert.Equal(tenantId.ToString(), first.Stream.Realm);
        Assert.Equal(AuditorFeedbackThread.Area, first.Stream.Area);
        Assert.NotEqual(first.Id, otherThread.Id);
        Assert.NotEqual(first.Id, otherRequest.Id);
        Assert.NotEqual(first.Id, otherTenant.Id);
        Assert.Equal(requestId, first.RequestId);
        Assert.Equal(threadId, first.ThreadId);
    }

    [Fact]
    public void ShouldAppendRecordedFactAtRevisionOneGivenAdmittedComment()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var owner = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);
        var intent = NativeFacts.Ref("child-admission-intent.v1");
        var result = NativeFacts.Ref("unit-result.v1");
        var author = Uuid.CreateVersion4();

        // Act
        var outcome = owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), CommandKind, null), intent, author,
            NativeFacts.Now, result);

        // Assert
        Assert.True(outcome.IsSuccess, outcome.Error?.Message);
        var fact = Assert.IsType<AuditorRequestCommentRecorded>(Assert.Single(Facts(owner)));
        Assert.Equal(tenantId, fact.TenantId);
        Assert.Equal(threadId, fact.SubjectId);
        Assert.Equal(1, fact.Revision);
        Assert.Equal(author, fact.ActorMemberId);
        Assert.Equal(intent, fact.Change);
        Assert.Equal(NativeFacts.Now, fact.OccurredAt);
        Assert.Null(fact.FirstResponse);
        Assert.Equal(result, fact.OperationResult);
        Assert.True(owner.IsMaterialized);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(fact, owner.Recorded);
    }

    [Fact]
    public void ShouldRefuseMaterializationAndAppendNothingGivenOperationOnAnotherStream()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var owner = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);
        var elsewhere = new AuditorFeedbackThread(tenantId, programId, requestId, Uuid.CreateVersion4());
        var foreign = NativeFacts.Root(elsewhere, Uuid.CreateVersion4(), CommandKind, null);

        // Act
        var result = owner.Materialize(foreign, NativeFacts.Ref(), Uuid.CreateVersion4(), NativeFacts.Now,
            NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(Facts(owner));
        Assert.False(owner.IsMaterialized);
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.Recorded);
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenThreadAlreadyRecorded()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var owner = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);
        Assert.True(Materialize(owner).IsSuccess);

        // Act
        var prepared = owner.PrepareMaterialize();
        var again = Materialize(owner);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAcceptMaterializationGivenFreshThread()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var owner = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);

        // Act
        var prepared = owner.PrepareMaterialize();

        // Assert
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldHydrateMaterializedThreadGivenReplayOfRecordedFact()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var history = History(tenantId, programId, requestId, threadId, Uuid.CreateVersion4());

        // Act
        var replayed = NativeFacts.Replay(new AuditorFeedbackThread(tenantId, programId, requestId, threadId), history);

        // Assert
        Assert.True(replayed.IsMaterialized);
        Assert.Equal(1, replayed.Revision);
        Assert.Equal(threadId, replayed.Recorded!.SubjectId);
        Assert.Equal(tenantId, replayed.Recorded.TenantId);
        Assert.Empty(Facts(replayed));
    }

    [Fact]
    public void ShouldRefuseMaterializationAndAppendNothingGivenReplayedThread()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var history = History(tenantId, programId, requestId, threadId, Uuid.CreateVersion4());
        var replayed = NativeFacts.Replay(new AuditorFeedbackThread(tenantId, programId, requestId, threadId), history);

        // Act
        var result = Materialize(replayed);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(Facts(replayed));
        Assert.Equal(1, replayed.Revision);
    }

    [Fact]
    public void ShouldDecideReplayGivenExactBindingOfRecordedOperation()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var logicalId = Uuid.CreateVersion4();
        var history = History(tenantId, programId, requestId, threadId, logicalId);
        var replayed = NativeFacts.Replay(new AuditorFeedbackThread(tenantId, programId, requestId, threadId, logicalId),
            history);

        // Act
        var decision = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(replayed.Acceptance.SelectedProvenance!));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Same(replayed.Recorded, replayed.Acceptance.SelectedFact);
    }

    [Fact]
    public void ShouldDecideConflictGivenChangedIntentOfRecordedOperation()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var logicalId = Uuid.CreateVersion4();
        var history = History(tenantId, programId, requestId, threadId, logicalId);
        var replayed = NativeFacts.Replay(new AuditorFeedbackThread(tenantId, programId, requestId, threadId, logicalId),
            history);
        var changed = AcceptedOperationBinding.Of(replayed.Acceptance.SelectedProvenance! with
        {
            AuthoredIntentSha256 = new string('c', 64)
        });

        // Act
        var decision = replayed.Acceptance.Decide(changed);

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldDecideNoneGivenThreadWithoutRecordedOperation()
    {
        // Arrange
        var (tenantId, programId, requestId, threadId) = Ids();
        var logicalId = Uuid.CreateVersion4();
        var owner = new AuditorFeedbackThread(tenantId, programId, requestId, threadId, logicalId);
        var candidate = AcceptedOperationBinding.Of(NativeFacts.Root(owner, logicalId, CommandKind, null));

        // Act
        var decision = owner.Acceptance.Decide(candidate);

        // Assert
        Assert.Equal(NativeAcceptanceDecision.None, decision);
    }

    static (Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ThreadId) Ids() => (Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static Result Materialize(AuditorFeedbackThread owner) => owner.Materialize(
        NativeFacts.Root(owner, Uuid.CreateVersion4(), CommandKind, null), NativeFacts.Ref(), Uuid.CreateVersion4(),
        NativeFacts.Now, NativeFacts.Ref());

    static DomainEvent[] Facts(AuditorFeedbackThread owner) =>
        new AggregateScenario<AuditorFeedbackThread>(owner).PendingEvents.ToArray();

    /// <summary>The valid history of a thread: its one materialization fact, built on a single seed owner.</summary>
    static DomainEvent[] History(Uuid tenantId, Uuid programId, Uuid requestId, Uuid threadId, Uuid logicalId)
    {
        var seed = new AuditorFeedbackThread(tenantId, programId, requestId, threadId);
        var outcome = seed.Materialize(NativeFacts.Root(seed, logicalId, CommandKind, null),
            NativeFacts.Ref("child-admission-intent.v1"), Uuid.CreateVersion4(), NativeFacts.Now, NativeFacts.Ref());
        Assert.True(outcome.IsSuccess, outcome.Error?.Message);
        return NativeFacts.Raised(seed);
    }
}
