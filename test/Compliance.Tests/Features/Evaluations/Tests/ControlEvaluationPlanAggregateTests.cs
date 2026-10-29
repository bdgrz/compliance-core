using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

public sealed class ControlEvaluationPlanAggregateTests
{
    [Fact]
    public void ShouldKeepOneStreamPerControlGivenDifferentProgramsAndExpectedVersions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();

        // Act
        var first = new ControlEvaluationPlan(tenantId, programId, controlId);
        var other = new ControlEvaluationPlan(tenantId, Uuid.CreateVersion4(), controlId);
        var elsewhere = new ControlEvaluationPlan(tenantId, programId, Uuid.CreateVersion4());
        var sameOperation = ControlEvaluationPlan.OperationId(tenantId, programId, controlId, 3);

        // Assert
        Assert.Equal(first.Stream, other.Stream);
        Assert.Equal(controlId, first.Id);
        Assert.NotEqual(first.Stream, elsewhere.Stream);
        Assert.Equal(sameOperation, ControlEvaluationPlan.OperationId(tenantId, programId, controlId, 3));
        Assert.NotEqual(sameOperation, ControlEvaluationPlan.OperationId(tenantId, programId, controlId, 4));
    }

    [Fact]
    public void ShouldAppendFirstVersionGivenEmptyPlanAndOriginalExpectedVersionZero()
    {
        // Arrange
        var owner = NewPlan();
        var logicalId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();

        // Act
        var result = Define(owner, logicalId, 0, controlVersionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<ControlEvaluationPlanVersionDefined>(Assert.Single(Facts(owner)));
        Assert.Equal(1, fact.Version);
        Assert.Equal(logicalId, fact.PlanVersionId);
        Assert.Equal(controlVersionId, fact.ControlVersionId);
        Assert.Equal(owner.Id, fact.ControlId);
        Assert.Equal(owner.ProgramId, fact.ProgramId);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenStaleExpectedVersion()
    {
        // Arrange
        var owner = NewPlan();
        Assert.True(Define(owner, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4()).IsSuccess);

        // Act
        var result = Define(owner, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendSuccessorGivenExpectedVersionEqualToCurrentRevision()
    {
        // Arrange
        var owner = NewPlan();
        Assert.True(Define(owner, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4()).IsSuccess);
        var successorVersion = Uuid.CreateVersion4();

        // Act
        var result = Define(owner, Uuid.CreateVersion4(), 1, successorVersion);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var facts = Facts(owner);
        Assert.Equal(2, facts.Length);
        Assert.Equal(2, Assert.IsType<ControlEvaluationPlanVersionDefined>(facts[1]).Version);
        Assert.Equal(successorVersion, owner.Latest?.ControlVersionId);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenMissingOriginalExpectedVersion()
    {
        // Arrange
        var owner = NewPlan();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(DefineControlEvaluationPlan), null);

        // Act
        var result = owner.Define(operation, Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenOperationOfAnotherOwner()
    {
        // Arrange
        var owner = NewPlan();
        var foreign = NewPlan();
        var operation = NativeFacts.Root(foreign, Uuid.CreateVersion4(), nameof(DefineControlEvaluationPlan), 0);

        // Act
        var result = owner.Define(operation, Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenOperationOfAnotherCommand()
    {
        // Arrange
        var owner = NewPlan();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "SomeOtherCommand", 0);

        // Act
        var result = owner.Define(operation, Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseAndAppendNothingGivenEmptyControlVersion()
    {
        // Arrange
        var owner = NewPlan();

        // Act
        var result = Define(owner, Uuid.CreateVersion4(), 0, Uuid.Empty);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldReportNotFoundGivenPlanLineOfAnotherProgram()
    {
        // Arrange
        var source = NewPlan();
        Assert.True(Define(source, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4()).IsSuccess);
        var history = NativeFacts.Raised(source);
        var tenantId = source.TenantId;
        var owner = NativeFacts.Replay(new ControlEvaluationPlan(tenantId, Uuid.CreateVersion4(), source.Id), history);

        // Act
        var prepared = owner.PrepareDefine(1);
        var result = Define(owner, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, prepared.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRestoreRevisionAndLatestGivenValidHistoryReplay()
    {
        // Arrange
        var source = NewPlan();
        Assert.True(Define(source, Uuid.CreateVersion4(), 0, Uuid.CreateVersion4()).IsSuccess);
        var secondControlVersion = Uuid.CreateVersion4();
        Assert.True(Define(source, Uuid.CreateVersion4(), 1, secondControlVersion).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new ControlEvaluationPlan(source.TenantId, source.ProgramId, source.Id), history);

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.Equal(secondControlVersion, replayed.Latest?.ControlVersionId);
        Assert.Equal(source.ProgramId, replayed.ProgramId);
    }

    [Fact]
    public void ShouldResolveOriginalAcceptanceGivenReplayAfterSuccessorAndRefuseChangedIntent()
    {
        // Arrange
        var source = NewPlan();
        var firstLogicalId = Uuid.CreateVersion4();
        var firstOperation = NativeFacts.Root(source, firstLogicalId, nameof(DefineControlEvaluationPlan), 0);
        Assert.True(source.Define(firstOperation, Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref()).IsSuccess);
        Assert.True(Define(source, Uuid.CreateVersion4(), 1, Uuid.CreateVersion4()).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(
            new ControlEvaluationPlan(source.TenantId, source.ProgramId, source.Id, firstLogicalId), history);
        var original = replayed.Acceptance.SelectedProvenance!;
        var exact = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(original));
        var changed = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(
            original with { AuthoredIntentSha256 = new string('c', 64) }));

        // Assert
        var fact = Assert.IsType<ControlEvaluationPlanVersionDefined>(replayed.Acceptance.SelectedFact);
        Assert.Equal(1, fact.Version);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(2, replayed.Revision);
    }

    static ControlEvaluationPlan NewPlan() =>
        new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static Result Define(ControlEvaluationPlan owner, Uuid logicalId, long expected, Uuid controlVersionId) =>
        owner.Define(NativeFacts.Root(owner, logicalId, nameof(DefineControlEvaluationPlan), expected), controlVersionId,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

    static DomainEvent[] Facts(ControlEvaluationPlan owner) =>
        new AggregateScenario<ControlEvaluationPlan>(owner).PendingEvents.ToArray();
}
