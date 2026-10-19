using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ControlEvaluationTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid OtherControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid EvaluationId = Uuid.CreateVersion4();
    static readonly Uuid PlanVersionId = Uuid.CreateVersion4();
    static readonly Uuid EvaluatorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset BeforePeriod = new(2026, 1, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AfterPeriod = new(2026, 2, 1, 0, 1, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureEvaluationAsOfTypeIDateGivenSubmissionAfterPeriodEnd()
    {
        // Arrange
        var beforeSubmission = await Fixture([
            (Started(BeforePeriod), BeforePeriod),
            (StepRecorded(BeforePeriod.AddHours(1), 0), BeforePeriod.AddHours(1)),
        ]);
        var afterSubmission = await Fixture([
            (Started(BeforePeriod), BeforePeriod),
            (StepRecorded(BeforePeriod.AddHours(1), 0), BeforePeriod.AddHours(1)),
            (StepRecorded(AfterPeriod, 1), AfterPeriod),
            (StepRecorded(AfterPeriod.AddMinutes(1), 2), AfterPeriod.AddMinutes(1)),
            (Submitted(AfterPeriod.AddMinutes(3),
                [BeforePeriod.AddHours(1), AfterPeriod, AfterPeriod.AddMinutes(1)]),
                AfterPeriod.AddMinutes(3)),
            (Reviewed(AfterPeriod.AddMinutes(4)), AfterPeriod.AddMinutes(4)),
        ]);

        // Act
        var original = await beforeSubmission.Contributor.ReadAsync(Engagement(),
            beforeSubmission.Context, CancellationToken.None);
        var withLaterDecisions = await afterSubmission.Contributor.ReadAsync(Engagement(),
            afterSubmission.Context, CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(withLaterDecisions.IsSuccess, withLaterDecisions.Error?.Message);
        var source = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(withLaterDecisions.Value.Sources);
        Assert.Equal("control_evaluations", original.Value.Family);
        Assert.Equal("control_evaluation", source.Kind);
        Assert.Equal(EvaluationId, source.RecordId);
        Assert.Equal(Uuid.CreateVersion5(EvaluationId, "control_evaluation_v1:2"),
            source.VersionId);
        Assert.Equal(2, source.Revision);
        Assert.Equal(source.VersionId, laterSource.VersionId);
        Assert.Equal(source.ContentSha256, laterSource.ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Contains(withLaterDecisions.Value.Blockers,
            blocker => blocker.Contains("accepted evaluation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldAcceptReviewedEffectiveEvaluationGivenMatchingControlVersion()
    {
        // Arrange
        var fixture = await Fixture(AcceptedHistory());

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("control_evaluations", result.Value.Family);
        Assert.Equal("control_evaluation", Assert.Single(result.Value.Sources).Kind);
        Assert.Empty(result.Value.Blockers);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldBlockControlGivenNoAcceptedEvaluationForEffectiveVersion()
    {
        // Arrange
        var fixture = await Fixture([], ControlVersionId);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains(ControlId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldRequireMatchingControlVersionGivenAcceptedEvaluationForPredecessor()
    {
        // Arrange
        var fixture = await Fixture(AcceptedHistory(), Uuid.CreateVersion4());

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("control version effective", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldRejectEffectiveConclusionGivenEveryStepWasNotTested()
    {
        // Arrange
        var at = BeforePeriod.AddMinutes(4);
        var fixture = await Fixture([
            (Started(BeforePeriod), BeforePeriod),
            (StepRecorded(BeforePeriod.AddMinutes(1), 0, ControlEvaluationLedger.NotTested),
                BeforePeriod.AddMinutes(1)),
            (StepRecorded(BeforePeriod.AddMinutes(2), 1, ControlEvaluationLedger.NotTested),
                BeforePeriod.AddMinutes(2)),
            (StepRecorded(BeforePeriod.AddMinutes(3), 2, ControlEvaluationLedger.NotTested),
                BeforePeriod.AddMinutes(3)),
            (UnsubstantiatedSubmission(at), at),
        ]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldIgnoreOutOfScopeEvaluationGivenMalformedOtherControlHistory()
    {
        // Arrange
        var history = AcceptedHistory().ToList();
        var at = BeforePeriod.AddHours(3);
        history.Add((new ControlEvaluationStarted(TenantId, ProgramId, OtherControlId,
            Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), [], EvaluatorId,
            ActorReference.ForMember(EvaluatorId, "Evaluator"), at, null, []), at));
        var fixture = await Fixture(history);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
    }

    static IReadOnlyList<(DomainEvent Event, DateTimeOffset OccurredAt)> AcceptedHistory() =>
    [
        (Started(BeforePeriod), BeforePeriod),
        (StepRecorded(BeforePeriod.AddMinutes(1), 0), BeforePeriod.AddMinutes(1)),
        (StepRecorded(BeforePeriod.AddMinutes(2), 1), BeforePeriod.AddMinutes(2)),
        (StepRecorded(BeforePeriod.AddMinutes(3), 2), BeforePeriod.AddMinutes(3)),
        (Submitted(BeforePeriod.AddHours(1)), BeforePeriod.AddHours(1)),
        (Reviewed(BeforePeriod.AddHours(2)), BeforePeriod.AddHours(2)),
    ];

    static async Task<FixtureState> Fixture(
        IReadOnlyList<(DomainEvent Event, DateTimeOffset OccurredAt)> history,
        Uuid? baselineControlVersionId = null)
    {
        var store = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "control-evaluations",
            ProgramId.ToString());
        var attached = history.Select((item, index) => Attach(item.Event, (ulong)index + 1,
            item.OccurredAt)).ToArray();
        if (attached.Length > 0)
            await store.AppendAsync(stream, 0, attached);

        var controlSource = new TypeIBaselineSourceReference("control_version", ControlId,
            baselineControlVersionId ?? ControlVersionId, 1, new string('a', 64));
        return new FixtureState(new ControlEvaluationTypeIBaselineSourceContributor(store),
            new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), [controlSource]));
    }

    static DomainEvent Attach(DomainEvent domainEvent, ulong position, DateTimeOffset occurredAt) =>
        domainEvent switch
        {
            ControlEvaluationStarted started => DomainEventSeed.Attach(started, ProgramId, position,
                occurredOn: occurredAt),
            ControlEvaluationStepRecorded step => DomainEventSeed.Attach(step, ProgramId, position,
                occurredOn: occurredAt),
            ControlEvaluationSubmitted submitted => DomainEventSeed.Attach(submitted, ProgramId,
                position, occurredOn: occurredAt),
            ControlEvaluationReviewed reviewed => DomainEventSeed.Attach(reviewed, ProgramId,
                position, occurredOn: occurredAt),
            _ => throw new ArgumentOutOfRangeException(nameof(domainEvent)),
        };

    static ControlEvaluationStarted Started(DateTimeOffset at)
    {
        return new ControlEvaluationStarted(TenantId, ProgramId, ControlId, EvaluationId, 1,
            ControlVersionId, ProcedureSteps(), EvaluatorId,
            ActorReference.ForMember(EvaluatorId, "Evaluator"),
            at, null, [])
        {
            PlanVersionId = PlanVersionId,
            PlanVersion = 1,
        };
    }

    static ControlEvaluationStepRecorded StepRecorded(DateTimeOffset at, int index,
        string result = ControlEvaluationLedger.Met)
    {
        var step = ProcedureSteps()[index];
        var items = result == ControlEvaluationLedger.NotTested
            ? []
            : step.InspectedItems.ToArray();
        var actor = ActorReference.ForMember(EvaluatorId, "Evaluator");
        return new ControlEvaluationStepRecorded(TenantId, ProgramId, ControlId, EvaluationId,
            index + 2, ControlEvaluationLedger.StepId(EvaluationId, index),
            new EvaluationStepResultView(1, result,
                result == ControlEvaluationLedger.NotTested
                    ? "No items were available."
                    : "The control operated.",
                items, actor, at), null);
    }

    static ControlEvaluationSubmitted Submitted(DateTimeOffset at,
        IReadOnlyList<DateTimeOffset>? recordedAt = null)
    {
        var actor = ActorReference.ForMember(EvaluatorId, "Evaluator");
        var steps = ProcedureSteps().Select((procedure, index) =>
        {
            var result = new EvaluationStepResultView(1, ControlEvaluationLedger.Met,
                "The control operated.", procedure.InspectedItems, actor,
                recordedAt is null ? BeforePeriod.AddMinutes(index + 1) : recordedAt[index]);
            return new ControlEvaluationStepView(ControlEvaluationLedger.StepId(EvaluationId, index),
                index, procedure.Assertion, procedure.Method, procedure.InspectedItems,
                procedure.ExpectedCondition, result);
        }).ToArray();
        var conclusions = ControlEvaluationLedger.Assertions.Select(assertion =>
            new EvaluationAssertionConclusion(assertion, ControlEvaluationLedger.Effective,
                "The assertion is supported.")).ToArray();
        var submission = new EvaluationSubmissionView(1, conclusions,
            ControlEvaluationLedger.Effective, steps, [], actor, at);
        return new ControlEvaluationSubmitted(TenantId, ProgramId, ControlId, EvaluationId, 5,
            submission);
    }

    static ControlEvaluationReviewed Reviewed(DateTimeOffset at) => new(TenantId, ProgramId,
        ControlId, EvaluationId, 6,
        new ControlEvaluationReviewView(Uuid.CreateVersion4(), 1, ControlEvaluationLedger.Accepted,
            "The evaluation is accepted.", ReviewerId,
            ActorReference.ForMember(ReviewerId, "Reviewer"), at));

    static ControlEvaluationSubmitted UnsubstantiatedSubmission(DateTimeOffset at)
    {
        var actor = ActorReference.ForMember(EvaluatorId, "Evaluator");
        var steps = ProcedureSteps().Select((procedure, index) =>
            new ControlEvaluationStepView(ControlEvaluationLedger.StepId(EvaluationId, index), index,
                procedure.Assertion, procedure.Method, procedure.InspectedItems,
                procedure.ExpectedCondition,
                new EvaluationStepResultView(1, ControlEvaluationLedger.NotTested,
                    "No items were available.", [], actor, BeforePeriod.AddMinutes(index + 1)))).ToArray();
        var conclusions = ControlEvaluationLedger.Assertions.Select(assertion =>
            new EvaluationAssertionConclusion(assertion, ControlEvaluationLedger.Effective,
                "The assertion is supported.")).ToArray();
        return new ControlEvaluationSubmitted(TenantId, ProgramId, ControlId, EvaluationId, 5,
            new EvaluationSubmissionView(1, conclusions, ControlEvaluationLedger.Effective, steps,
                [], actor, at));
    }

    static EvaluationProcedureStep[] ProcedureSteps() => ControlEvaluationLedger.Assertions
        .Select(ProcedureStep).ToArray();

    static EvaluationProcedureStep ProcedureStep(string assertion) => new(assertion, "inspection",
        [new EvaluationInspectedItem("control", ControlId.ToString(), ControlVersionId.ToString())],
        "The control meets its documented requirement.");

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", new DateOnly(2026, 1, 31), null,
        ["security"], Uuid.CreateVersion4(), [], "draft", 1, BeforePeriod, ReviewerId, "Examiner");

    sealed record FixtureState(ControlEvaluationTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);
}
