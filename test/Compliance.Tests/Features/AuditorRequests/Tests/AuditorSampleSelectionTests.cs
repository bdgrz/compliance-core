using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

/// <summary>
///     Owner-level tests of the auditor sample selection: one materialization at revision one on its own stream,
///     refusals that append nothing, exact retry and changed intent, and replay of valid history.
/// </summary>
public sealed class AuditorSampleSelectionTests
{
    static readonly DateTimeOffset RecordedAt = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    static AuditorSampleSelection Open(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectionId,
        Uuid selectedLogicalId = default) =>
        new(tenantId, programId, requestId, selectionId, selectedLogicalId);

    static OperationProvenance Operation(AuditorSampleSelection owner, Uuid logicalId) =>
        NativeFacts.Root(owner, logicalId, "MaterializeAuditorSampleSelection", null, null, RecordedAt);

    static Result Materialize(AuditorSampleSelection owner, OperationProvenance operation, Uuid? actor = null) =>
        owner.Materialize(operation, NativeFacts.Ref(), actor ?? Uuid.CreateVersion4(), RecordedAt, NativeFacts.Ref());

    static int Pending(AuditorSampleSelection owner) =>
        new AggregateScenario<AuditorSampleSelection>(owner).PendingEvents.Count;

    static SampleSelectionRecorded RecordedFact(AuditorSampleSelection owner) =>
        Assert.IsType<SampleSelectionRecorded>(
            Assert.Single(new AggregateScenario<AuditorSampleSelection>(owner).PendingEvents));

    static AuditorSampleSelection Materialized(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectionId,
        Uuid logicalId, Uuid selectedLogicalId, out OperationProvenance operation)
    {
        var seed = Open(tenantId, programId, requestId, selectionId);
        operation = Operation(seed, logicalId);
        Assert.True(Materialize(seed, operation).IsSuccess);
        return NativeFacts.Replay(Open(tenantId, programId, requestId, selectionId, selectedLogicalId),
            NativeFacts.Raised(seed));
    }

    [Fact]
    public void ShouldDeriveStreamFromTenantProgramRequestAndSelectionGivenOpenSelection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var selectionId = Uuid.CreateVersion4();

        // Act
        var first = Open(tenantId, programId, requestId, selectionId, Uuid.CreateVersion4());
        var second = Open(tenantId, programId, requestId, selectionId);
        var otherRequest = Open(tenantId, programId, Uuid.CreateVersion4(), selectionId);
        var otherSelection = Open(tenantId, programId, requestId, Uuid.CreateVersion4());

        // Assert
        var identity = AuditorSampleSelection.IdentityOf(tenantId, programId, requestId, selectionId);
        Assert.Equal(identity, first.Id);
        Assert.Equal(first.Stream, second.Stream);
        Assert.Equal(new EventStreamAddress(tenantId.ToString(), AuditorSampleSelection.Area, identity.ToString()),
            first.Stream);
        Assert.NotEqual(first.Stream, otherRequest.Stream);
        Assert.NotEqual(first.Stream, otherSelection.Stream);
        Assert.Equal(0, first.Revision);
        Assert.False(first.IsMaterialized);
        Assert.Null(first.Recorded);
    }

    [Fact]
    public void ShouldMaterializeRevisionOneGivenOwnStreamOperation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var selectionId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var owner = Open(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), selectionId);
        var operation = Operation(owner, Uuid.CreateVersion4());
        var intent = NativeFacts.Ref("sample-selection-intent.v1");
        var unitResult = NativeFacts.Ref("unit-result.v1");

        // Act
        var result = owner.Materialize(operation, intent, actor, RecordedAt, unitResult);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var recorded = RecordedFact(owner);
        Assert.Equal(tenantId, recorded.TenantId);
        Assert.Equal(selectionId, recorded.SubjectId);
        Assert.Equal(1, recorded.Revision);
        Assert.Equal(actor, recorded.ActorMemberId);
        Assert.Equal(RecordedAt, recorded.OccurredAt);
        Assert.Equal(operation, recorded.Operation);
        Assert.Equal(intent, recorded.Change);
        Assert.Equal(unitResult, recorded.OperationResult);
        Assert.Null(recorded.FirstResponse);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsMaterialized);
        Assert.Same(recorded, owner.Recorded);
    }

    [Fact]
    public void ShouldAppendNothingGivenOperationNotRootedOnSelectionStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var owner = Open(tenantId, programId, requestId, Uuid.CreateVersion4());
        var elsewhere = Open(tenantId, programId, requestId, Uuid.CreateVersion4());

        // Act
        var result = Materialize(owner, Operation(elsewhere, Uuid.CreateVersion4()));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(0, owner.Revision);
        Assert.False(owner.IsMaterialized);
    }

    [Fact]
    public void ShouldRefuseNullArgumentsGivenMaterializationAndAppendNothing()
    {
        // Arrange
        var owner = Open(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var operation = Operation(owner, Uuid.CreateVersion4());
        var reference = NativeFacts.Ref();
        var actor = Uuid.CreateVersion4();

        // Act
        var noOperation = Record.Exception(() =>
        {
            _ = owner.Materialize(null!, reference, actor, RecordedAt, reference);
        });
        var noIntent = Record.Exception(() =>
        {
            _ = owner.Materialize(operation, null!, actor, RecordedAt, reference);
        });
        var noResult = Record.Exception(() =>
        {
            _ = owner.Materialize(operation, reference, actor, RecordedAt, null!);
        });

        // Assert
        Assert.IsType<ArgumentNullException>(noOperation);
        Assert.IsType<ArgumentNullException>(noIntent);
        Assert.IsType<ArgumentNullException>(noResult);
        Assert.Equal(0, Pending(owner));
        Assert.False(owner.IsMaterialized);
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenMaterializedSelectionAndAppendNothing()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Materialized(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), logicalId, logicalId, out var operation);
        var firstFact = owner.Recorded;

        // Act
        var exactRetry = Materialize(owner, operation);
        var otherOperation = Materialize(owner, Operation(owner, Uuid.CreateVersion4()));
        var prepared = owner.PrepareMaterialize();

        // Assert
        Assert.False(exactRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, exactRetry.Error!.Kind);
        Assert.False(otherOperation.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, otherOperation.Error!.Kind);
        Assert.False(prepared.IsSuccess);
        Assert.Equal(0, Pending(owner));
        Assert.Same(firstFact, owner.Recorded);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReplayMaterializedSelectionGivenValidHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var selectionId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();

        // Act
        var replayed = Materialized(tenantId, programId, requestId, selectionId, logicalId, logicalId, out var operation);

        // Assert
        Assert.True(replayed.IsMaterialized);
        Assert.Equal(1, replayed.Revision);
        Assert.Equal(requestId, replayed.RequestId);
        Assert.Equal(selectionId, replayed.SelectionId);
        Assert.Equal(selectionId, replayed.Recorded!.SubjectId);
        Assert.Equal(operation.LogicalId, replayed.Recorded.Operation.LogicalId);
        Assert.Same(replayed.Recorded, replayed.Acceptance.SelectedFact);
        Assert.Equal(0, Pending(replayed));
    }

    [Fact]
    public void ShouldDecideReplayedGivenExactOperationAndConflictGivenChangedIntent()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Materialized(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), logicalId, logicalId, out var operation);
        var changed = operation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var exact = owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
        var different = owner.Acceptance.Decide(AcceptedOperationBinding.Of(changed));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, different);
    }

    [Fact]
    public void ShouldDecideNoneGivenUnrelatedLogicalOperationOnMaterializedSelection()
    {
        // Arrange
        var other = Uuid.CreateVersion4();
        var owner = Materialized(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), other, out _);
        var candidate = Operation(owner, other);

        // Act
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(candidate));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.None, decision);
        Assert.Null(owner.Acceptance.SelectedFact);
        Assert.True(owner.IsMaterialized);
    }
}
