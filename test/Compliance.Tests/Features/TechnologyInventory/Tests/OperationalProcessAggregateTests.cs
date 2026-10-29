using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class OperationalProcessAggregateTests
{
    [Fact]
    public void ShouldRestoreRevisionAndLifecycleGivenValidRecordedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var processId = Uuid.CreateVersion4();
        var history = NativeFacts.Raised(NamedRecordFacts.RecordedProcess(tenantId, processId, 2));

        // Act
        var replayed = NativeFacts.Replay(new OperationalProcess(tenantId, processId), history);

        // Assert
        Assert.Equal(4, history.Length);
        Assert.Equal(2, replayed.Revision);
        Assert.Equal(2, replayed.Generation);
        Assert.Null(replayed.Pending);
        Assert.Equal(2, replayed.Current?.Revision);
        Assert.Equal(1, replayed.At(1)?.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange()
    {
        // Arrange
        var owner = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(owner.AcceptChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Record", null), true, 0,
            NamedRecordFacts.Ref("named-record-change-intent.v1"), NamedRecordFacts.Ref("first-response.v1")).IsSuccess);

        // Act
        var result = owner.AcceptChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Revise", 0), false, 0,
            NamedRecordFacts.Ref("named-record-change-intent.v1"), NamedRecordFacts.Ref("first-response.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(new AggregateScenario<OperationalProcess>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedProcess()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2);
        var before = new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count;

        // Act
        var result = owner.AcceptChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Revise", 1), false, 1,
            NamedRecordFacts.Ref("named-record-change-intent.v1"), NamedRecordFacts.Ref("first-response.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(owner.Pending);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(before, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenExistingIdentity()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count;

        // Act
        var result = owner.AcceptChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Record", null), true, 0,
            NamedRecordFacts.Ref("named-record-change-intent.v1"), NamedRecordFacts.Ref("first-response.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenUnrecordedProcess()
    {
        // Arrange
        var owner = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = owner.AcceptChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Revise", 1), false, 1,
            NamedRecordFacts.Ref("named-record-change-intent.v1"), NamedRecordFacts.Ref("first-response.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<OperationalProcess>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseApplyAndKeepPendingGivenChangedIntent()
    {
        // Arrange
        var owner = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = NativeFacts.Root(owner, Uuid.CreateVersion4(), "Record", null);
        var intent = NamedRecordFacts.Ref("named-record-change-intent.v1");
        Assert.True(owner.AcceptChange(root, true, 0, intent, NamedRecordFacts.Ref("first-response.v1")).IsSuccess);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Tester");

        // Act
        var changed = owner.ApplyChange(Child(root, "ApplyOperationalProcessChange"),
            NamedRecordFacts.Ref("named-record-change-intent.v1"),
            NamedRecordFacts.Ref(OperationalProcessRevisionRecorded.ContentSchema), actor, NativeFacts.Now,
            NamedRecordFacts.Ref("mutation-result.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.NotNull(owner.Pending);
        Assert.Equal(0, owner.Revision);
        Assert.Single(new AggregateScenario<OperationalProcess>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSecondApplyAndAppendNothingGivenCommittedRevision()
    {
        // Arrange
        var owner = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = NativeFacts.Root(owner, Uuid.CreateVersion4(), "Record", null);
        var intent = NamedRecordFacts.Ref("named-record-change-intent.v1");
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Tester");
        Assert.True(owner.AcceptChange(root, true, 0, intent, NamedRecordFacts.Ref("first-response.v1")).IsSuccess);
        var content = NamedRecordFacts.Ref(OperationalProcessRevisionRecorded.ContentSchema);
        Assert.True(owner.ApplyChange(Child(root, "ApplyOperationalProcessChange"), intent, content, actor,
            NativeFacts.Now, NamedRecordFacts.Ref("mutation-result.v1")).IsSuccess);

        // Act
        var retried = owner.ApplyChange(Child(root, "ApplyOperationalProcessChange"), intent, content, actor,
            NativeFacts.Now, NamedRecordFacts.Ref("mutation-result.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, retried.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(2, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldFenceApplyAndClearPendingGivenCanceledChange()
    {
        // Arrange
        var owner = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = NativeFacts.Root(owner, Uuid.CreateVersion4(), "Record", null);
        var intent = NamedRecordFacts.Ref("named-record-change-intent.v1");
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Tester");
        Assert.True(owner.AcceptChange(root, true, 0, intent, NamedRecordFacts.Ref("first-response.v1")).IsSuccess);

        // Act
        var canceled = owner.CancelChange(Child(root, "CancelOperationalProcessChange"),
            NamedRecordFacts.Ref("failure-proof.v1"));
        var late = owner.ApplyChange(Child(root, "ApplyOperationalProcessChange"), intent,
            NamedRecordFacts.Ref(OperationalProcessRevisionRecorded.ContentSchema), actor, NativeFacts.Now,
            NamedRecordFacts.Ref("mutation-result.v1"));

        // Assert
        Assert.True(canceled.IsSuccess);
        Assert.Null(owner.Pending);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.Equal(2, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseCancelAndAppendNothingGivenNoPendingChange()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count;

        // Act
        var result = owner.CancelChange(NativeFacts.Root(owner, Uuid.CreateVersion4(), "Cancel", null),
            NamedRecordFacts.Ref("failure-proof.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAcceptNextChangeAtNewGenerationGivenCanceledRevisionChange()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var intent = NamedRecordFacts.Ref("named-record-change-intent.v1");
        var root = NativeFacts.Root(owner, Uuid.CreateVersion4(), "Revise", 1);
        Assert.True(owner.AcceptChange(root, false, 1, intent, NamedRecordFacts.Ref("first-response.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Child(root, "CancelOperationalProcessChange"),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = NativeFacts.Root(owner, Uuid.CreateVersion4(), "Revise", 1);

        // Act
        var accepted = owner.AcceptChange(next, false, 1, intent, NamedRecordFacts.Ref("first-response.v1"));

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.Equal(3, owner.Generation);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsPending(next.RootOperationId, 3));
        Assert.False(owner.IsPending(root.RootOperationId, 2));
        Assert.Equal(5, new AggregateScenario<OperationalProcess>(owner).PendingEvents.Count);
    }

    static OperationProvenance Child(OperationProvenance root, string commandKind) =>
        new(Uuid.CreateVersion4(), root.RootOperationId, null, commandKind, root.Actor, root.Owner, "current", null,
            root.AuthoredIntentSha256, root.AcceptedAt, root.Owner, root.Owner);
}
