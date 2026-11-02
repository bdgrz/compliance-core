using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

/// <summary>Aggregate-level coverage of the program readiness governor, driven through its own commands.</summary>
public sealed class ProgramReadinessTests
{
    const string RunKind = "RunReadinessAssessment";
    const string DecideKind = "DecideReadiness";

    [Fact]
    public void ShouldDeriveOneIdentityGivenSameTenantAndProgram()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();

        // Act
        var first = new ProgramReadiness(tenant, program);
        var second = new ProgramReadiness(tenant, program, Uuid.CreateVersion4());
        var other = new ProgramReadiness(tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Stream, second.Stream);
        Assert.NotEqual(first.Id, other.Id);
    }

    [Fact]
    public void ShouldAppendNothingGivenRunWithStaleRevision()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var operation = Op(owner, RunKind, 5);

        // Act
        var result = owner.RequestRun(operation, Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(0, owner.AdoptionGeneration);
    }

    [Fact]
    public void ShouldAppendNothingGivenRunOwnedByAnotherStream()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var foreign = Op(Open().Aggregate, RunKind, 0);

        // Act
        var result = owner.RequestRun(foreign, Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReserveOnePendingGenerationGivenFirstRun()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var operation = Op(owner, RunKind, 0);
        var intent = Ref();

        // Act
        var result = owner.RequestRun(operation, intent, Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var requested = Assert.IsType<ReadinessAssessmentRequested>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(1, requested.Revision);
        Assert.Equal(1, requested.Generation);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.AdoptionGeneration);
        Assert.Same(requested, owner.PendingAdoption);
        Assert.Null(owner.LatestAssessmentId);
    }

    [Fact]
    public void ShouldRefuseSecondRunWithoutFactGivenPendingAdoption()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        _ = Run(owner);
        var second = Op(owner, RunKind, (ulong)owner.Revision);

        // Act
        var result = owner.RequestRun(second, Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Single(scenario.PendingEvents);
        Assert.Equal(1, owner.AdoptionGeneration);
    }

    [Fact]
    public void ShouldRefuseAdoptionWithoutFactGivenGenerationOrIntentOfAnotherRun()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        var adoption = Child(run.Operation);

        // Act
        var wrongGeneration = owner.Adopt(adoption, run.Intent, owner.AdoptionGeneration + 1, Ref(), Ref());
        var wrongIntent = owner.Adopt(adoption, Ref(), owner.AdoptionGeneration, Ref(), Ref());

        // Assert
        Assert.False(wrongGeneration.IsSuccess);
        Assert.False(wrongIntent.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.NotNull(owner.PendingAdoption);
        Assert.Null(owner.LatestAssessmentId);
    }

    [Fact]
    public void ShouldRefuseAdoptionWithoutFactGivenNothingPending()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var again = Child(run.Operation);

        // Act
        var result = owner.Adopt(again, run.Intent, 1, Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.Equal(run.Operation.RootOperationId, owner.LatestAssessmentId);
    }

    [Fact]
    public void ShouldMakeAssessmentLatestAndClearPendingGivenExactAdoption()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);

        // Act
        var adopted = Adopt(owner, run);

        // Assert
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.Same(adopted, owner.LatestAdoption);
        Assert.Null(owner.PendingAdoption);
        Assert.Equal(run.Operation.RootOperationId, owner.LatestAssessmentId);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(1, owner.AdoptionGeneration);
        Assert.Same(adopted, owner.AdoptionOf(run.Operation.RootOperationId));
    }

    [Fact]
    public void ShouldRefuseDecisionWithoutFactGivenUnknownAssessment()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var operation = Op(owner, DecideKind, (ulong)owner.Revision);

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, Uuid.CreateVersion4(), operation, Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.Null(owner.PendingDecision);
    }

    [Fact]
    public void ShouldRefuseDecisionWithoutFactGivenStaleRevision()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var operation = Op(owner, DecideKind, 1);

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, run.Operation.RootOperationId, operation,
            Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.Null(owner.PendingDecision);
    }

    [Fact]
    public void ShouldSealLatestAdoptionGenerationGivenAcceptedDecision()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var operation = Op(owner, DecideKind, (ulong)owner.Revision);
        var intent = Ref();

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, run.Operation.RootOperationId, operation,
            intent, Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var accepted = Assert.IsType<ReadinessDecisionAccepted>(scenario.PendingEvents[^1]);
        Assert.Equal(owner.AdoptionGeneration, accepted.Generation);
        Assert.Same(accepted, owner.PendingDecision);
        Assert.Equal((intent, 1L, ReadinessDecisionKind.Readiness), ProgramReadiness.DecisionTerms(accepted)!.Value);
        Assert.Same(accepted, owner.AcceptedDecision(operation.RootOperationId));
    }

    [Fact]
    public void ShouldRefuseRunAndSecondDecisionWithoutFactGivenPendingDecision()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        _ = Accept(owner, run, ReadinessDecisionKind.Readiness);
        var count = scenario.PendingEvents.Count;

        // Act
        var newRun = owner.RequestRun(Op(owner, RunKind, (ulong)owner.Revision), Ref(), Ref());
        var secondDecision = owner.AcceptDecision(ReadinessDecisionKind.TypeIEntry, run.Operation.RootOperationId,
            Op(owner, DecideKind, (ulong)owner.Revision), Ref(), Ref());

        // Assert
        Assert.False(newRun.IsSuccess);
        Assert.False(secondDecision.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseDecisionOfOlderAssessmentWithoutFactGivenLaterAdoption()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var first = Run(owner);
        _ = Adopt(owner, first);
        var second = Run(owner);
        _ = Adopt(owner, second);
        var count = scenario.PendingEvents.Count;
        var operation = Op(owner, DecideKind, (ulong)owner.Revision);

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, first.Operation.RootOperationId, operation,
            Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(second.Operation.RootOperationId, owner.LatestAssessmentId);
        Assert.Equal(2, owner.AdoptionGeneration);
    }

    [Fact]
    public void ShouldRefuseDecisionAsConflictWithoutFactGivenSelectedOlderAssessmentAfterLaterAdoption()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var probe = new ProgramReadiness(tenant, program);
        var firstRun = Op(probe, RunKind, 0);
        var firstIntent = Ref();
        var secondRun = Op(probe, RunKind, 2);
        var secondIntent = Ref();
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new ReadinessAssessmentRequested(probe.Stream, 1, 1, firstIntent, null, firstRun,
                Ref(), null), probe.Id, 1),
            DomainEventSeed.Attach(new ReadinessAssessmentAdopted(probe.Stream, 2, 1, firstIntent, Ref(),
                Child(firstRun), null, Ref()), probe.Id, 2),
            DomainEventSeed.Attach(new ReadinessAssessmentRequested(probe.Stream, 3, 2, secondIntent, null, secondRun,
                Ref(), null), probe.Id, 3),
            DomainEventSeed.Attach(new ReadinessAssessmentAdopted(probe.Stream, 4, 2, secondIntent, Ref(),
                Child(secondRun), null, Ref()), probe.Id, 4),
        };
        var scenario = new AggregateScenario<ProgramReadiness>(
            new ProgramReadiness(tenant, program, Uuid.Empty, Uuid.Empty, firstRun.RootOperationId)).Given(facts);
        var owner = scenario.Aggregate;
        var operation = Op(owner, DecideKind, 4);

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, firstRun.RootOperationId, operation, Ref(),
            Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Null(owner.PendingDecision);
        Assert.Equal(secondRun.RootOperationId, owner.LatestAssessmentId);
        Assert.Equal(4, owner.Revision);
    }

    [Fact]
    public void ShouldRecordReadinessDecisionAndFreePendingGivenExactCompletion()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var accepted = Accept(owner, run, ReadinessDecisionKind.Readiness);
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var completion = Child(((INativeOperationFact)accepted).Operation);

        // Act
        var result = owner.RecordReadinessDecision(completion, Ref(), run.Operation.RootOperationId, Ref(), terms.Intent,
            Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<ReadinessDecisionRecorded>(scenario.PendingEvents[^1]);
        Assert.Null(owner.PendingDecision);
        Assert.Same(recorded, owner.DecisionOf(run.Operation.RootOperationId));
        Assert.Equal(4, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondReadinessDecisionWithoutFactGivenAlreadyDecidedAssessment()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        Complete(owner, run, Accept(owner, run, ReadinessDecisionKind.Readiness));
        var count = scenario.PendingEvents.Count;

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.Readiness, run.Operation.RootOperationId,
            Op(owner, DecideKind, (ulong)owner.Revision), Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldKeepReadinessDecisionAndAllowTypeIEntryGivenDecidedAssessment()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        Complete(owner, run, Accept(owner, run, ReadinessDecisionKind.Readiness));

        // Act
        var accepted = Accept(owner, run, ReadinessDecisionKind.TypeIEntry);
        Complete(owner, run, accepted);

        // Assert
        Assert.NotNull(owner.DecisionOf(run.Operation.RootOperationId));
        Assert.NotNull(owner.CurrentEntryDecision);
        Assert.Null(owner.PendingDecision);
        Assert.Equal(6, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondTypeIEntryWithoutFactGivenEntryDecisionOnAssessment()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        Complete(owner, run, Accept(owner, run, ReadinessDecisionKind.TypeIEntry));
        var count = scenario.PendingEvents.Count;

        // Act
        var result = owner.AcceptDecision(ReadinessDecisionKind.TypeIEntry, run.Operation.RootOperationId,
            Op(owner, DecideKind, (ulong)owner.Revision), Ref(), Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseCompletionWithoutFactGivenNothingPendingOrChangedIntent()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var nothingPending = owner.RecordReadinessDecision(Child(run.Operation), Ref(), run.Operation.RootOperationId,
            Ref(), Ref(), Ref());
        var accepted = Accept(owner, run, ReadinessDecisionKind.Readiness);
        var completion = Child(((INativeOperationFact)accepted).Operation);
        var count = scenario.PendingEvents.Count;

        // Act
        var changedIntent = owner.RecordReadinessDecision(completion, Ref(), run.Operation.RootOperationId, Ref(), Ref(),
            Ref());
        var wrongKind = owner.RecordTypeIEntryDecision(completion, Ref(), run.Operation.RootOperationId, Ref(),
            ProgramReadiness.DecisionTerms(accepted)!.Value.Intent, Ref());

        // Assert
        Assert.False(nothingPending.IsSuccess);
        Assert.False(changedIntent.IsSuccess);
        Assert.False(wrongKind.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Same(accepted, owner.PendingDecision);
    }

    [Fact]
    public void ShouldRefuseCompletionWithoutFactGivenAssessmentOtherThanLatest()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var accepted = Accept(owner, run, ReadinessDecisionKind.Readiness);
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var completion = Child(((INativeOperationFact)accepted).Operation);
        var count = scenario.PendingEvents.Count;

        // Act
        var result = owner.RecordReadinessDecision(completion, Ref(), Uuid.CreateVersion4(), Ref(), terms.Intent, Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Same(accepted, owner.PendingDecision);
    }

    [Fact]
    public void ShouldCancelPendingDecisionAndFenceCompletionGivenExactFailure()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var accepted = Accept(owner, run, ReadinessDecisionKind.Readiness);
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var failure = Child(((INativeOperationFact)accepted).Operation);

        // Act
        var canceled = owner.CancelDecision(failure, Ref());
        var late = owner.RecordReadinessDecision(failure, Ref(), run.Operation.RootOperationId, Ref(), terms.Intent, Ref());
        var again = owner.CancelDecision(failure, Ref());

        // Assert
        Assert.True(canceled.IsSuccess);
        Assert.IsType<ReadinessDecisionCanceled>(scenario.PendingEvents[^1]);
        Assert.False(late.IsSuccess);
        Assert.False(again.IsSuccess);
        Assert.Equal(4, scenario.PendingEvents.Count);
        Assert.Null(owner.PendingDecision);
        Assert.Null(owner.DecisionOf(run.Operation.RootOperationId));
    }

    [Fact]
    public void ShouldAllowNewDecisionOnSameAssessmentGivenCanceledDecision()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var run = Run(owner);
        _ = Adopt(owner, run);
        var accepted = Accept(owner, run, ReadinessDecisionKind.Readiness);
        _ = owner.CancelDecision(Child(((INativeOperationFact)accepted).Operation), Ref());

        // Act
        var retry = owner.AcceptDecision(ReadinessDecisionKind.Readiness, run.Operation.RootOperationId,
            Op(owner, DecideKind, (ulong)owner.Revision), Ref(), Ref());

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.IsType<ReadinessDecisionAccepted>(scenario.PendingEvents[^1]);
        Assert.NotNull(owner.PendingDecision);
    }

    [Fact]
    public void ShouldResetDecisionsGivenLaterAdoption()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;
        var first = Run(owner);
        _ = Adopt(owner, first);
        Complete(owner, first, Accept(owner, first, ReadinessDecisionKind.Readiness));
        Complete(owner, first, Accept(owner, first, ReadinessDecisionKind.TypeIEntry));

        // Act
        var second = Run(owner);
        _ = Adopt(owner, second);

        // Assert
        Assert.Equal(second.Operation.RootOperationId, owner.LatestAssessmentId);
        Assert.Null(owner.DecisionOf(first.Operation.RootOperationId));
        Assert.Null(owner.DecisionOf(second.Operation.RootOperationId));
        Assert.Null(owner.CurrentEntryDecision);
        Assert.Equal(2, owner.AdoptionGeneration);
        Assert.Equal(8, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreStateAndRefuseRunsGivenValidHistoryReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var probe = new ProgramReadiness(tenant, program);
        var runOperation = Op(probe, RunKind, 0);
        var intent = Ref();
        var adoption = Child(runOperation);
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new ReadinessAssessmentRequested(probe.Stream, 1, 1, intent, null, runOperation, Ref(),
                null), probe.Id, 1),
            DomainEventSeed.Attach(new ReadinessAssessmentAdopted(probe.Stream, 2, 1, intent, Ref(), adoption, null,
                Ref()), probe.Id, 2),
        };
        var scenario = new AggregateScenario<ProgramReadiness>(new ProgramReadiness(tenant, program)).Given(facts);
        var owner = scenario.Aggregate;

        // Act
        var stale = owner.RequestRun(Op(owner, RunKind, 0), Ref(), Ref());
        var fresh = owner.RequestRun(Op(owner, RunKind, 2), Ref(), Ref());

        // Assert
        Assert.Equal(runOperation.RootOperationId, owner.LatestAssessmentId);
        Assert.Equal(2, owner.AdoptionGeneration);
        Assert.False(stale.IsSuccess);
        Assert.True(fresh.IsSuccess);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldReplayCommittedRunGivenExactRetryAfterLaterFacts()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var probe = new ProgramReadiness(tenant, program);
        var runOperation = Op(probe, RunKind, 0);
        var intent = Ref();
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new ReadinessAssessmentRequested(probe.Stream, 1, 1, intent, null, runOperation, Ref(),
                null), probe.Id, 1),
            DomainEventSeed.Attach(new ReadinessAssessmentAdopted(probe.Stream, 2, 1, intent, Ref(),
                Child(runOperation), null, Ref()), probe.Id, 2),
        };
        var owner = new AggregateScenario<ProgramReadiness>(
            new ProgramReadiness(tenant, program, runOperation.LogicalId)).Given(facts).Aggregate;

        // Act
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(runOperation));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Same(owner.Acceptance.SelectedFact, owner.RequestedRun(runOperation.RootOperationId));
    }

    [Fact]
    public void ShouldConflictGivenSameLogicalRunWithChangedIntent()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var probe = new ProgramReadiness(tenant, program);
        var runOperation = Op(probe, RunKind, 0);
        var owner = new AggregateScenario<ProgramReadiness>(
                new ProgramReadiness(tenant, program, runOperation.LogicalId))
            .Given(DomainEventSeed.Attach(new ReadinessAssessmentRequested(probe.Stream, 1, 1, Ref(), null, runOperation,
                Ref(), null), probe.Id, 1)).Aggregate;
        var changed = runOperation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var decision = owner.Acceptance.Decide(AcceptedOperationBinding.Of(changed));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldRetainBoundedStateGivenManyRunsAndDecisions()
    {
        // Arrange
        var scenario = Open();
        var owner = scenario.Aggregate;

        // Act
        for (var index = 0; index < 12; index++)
        {
            var run = Run(owner);
            _ = Adopt(owner, run);
            Complete(owner, run, Accept(owner, run, ReadinessDecisionKind.Readiness));
        }

        // Assert
        Assert.Equal(48, owner.Revision);
        Assert.Equal(12, owner.AdoptionGeneration);
        Assert.Null(owner.PendingAdoption);
        Assert.Null(owner.PendingDecision);
    }

    static AggregateScenario<ProgramReadiness> Open() =>
        new(new ProgramReadiness(Uuid.CreateVersion4(), Uuid.CreateVersion4()));

    static ImmutableContentRef Ref() => TenantFacts.Response();

    static OperationProvenance Op(ProgramReadiness owner, string kind, ulong expectedRevision) =>
        TenantFacts.Provenance(owner, kind, expectedRevision: expectedRevision);

    /// <summary>A later operation of the same root, such as the adoption or completion of an accepted run or decision.</summary>
    static OperationProvenance Child(OperationProvenance root) =>
        root with { LogicalId = Uuid.CreateVersion4(), CausationEventId = null };

    static (OperationProvenance Operation, ImmutableContentRef Intent) Run(ProgramReadiness owner)
    {
        var operation = Op(owner, RunKind, (ulong)owner.Revision);
        var intent = Ref();
        Assert.True(owner.RequestRun(operation, intent, Ref()).IsSuccess);
        return (operation, intent);
    }

    static ReadinessAssessmentAdopted Adopt(ProgramReadiness owner, (OperationProvenance Operation, ImmutableContentRef Intent) run)
    {
        Assert.True(owner.Adopt(Child(run.Operation), run.Intent, owner.AdoptionGeneration, Ref(), Ref()).IsSuccess);
        return owner.LatestAdoption!;
    }

    static DomainEvent Accept(ProgramReadiness owner, (OperationProvenance Operation, ImmutableContentRef Intent) run,
        ReadinessDecisionKind kind)
    {
        var result = owner.AcceptDecision(kind, run.Operation.RootOperationId, Op(owner, DecideKind, (ulong)owner.Revision),
            Ref(), Ref());
        Assert.True(result.IsSuccess);
        return owner.PendingDecision!;
    }

    static void Complete(ProgramReadiness owner, (OperationProvenance Operation, ImmutableContentRef Intent) run,
        DomainEvent accepted)
    {
        var terms = ProgramReadiness.DecisionTerms(accepted)!.Value;
        var completion = Child(((INativeOperationFact)accepted).Operation);
        var result = terms.Kind == ReadinessDecisionKind.Readiness
            ? owner.RecordReadinessDecision(completion, Ref(), run.Operation.RootOperationId, Ref(), terms.Intent, Ref())
            : owner.RecordTypeIEntryDecision(completion, Ref(), run.Operation.RootOperationId, Ref(), terms.Intent, Ref());
        Assert.True(result.IsSuccess);
    }
}
