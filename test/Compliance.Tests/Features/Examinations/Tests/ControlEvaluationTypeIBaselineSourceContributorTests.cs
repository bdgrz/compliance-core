using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.History;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

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
        var beforeSubmission = await Fixture(async history =>
        {
            await history.StartAsync(BeforePeriod);
            await history.StepAsync(BeforePeriod.AddHours(1), 0);
        });
        var afterSubmission = await Fixture(async history =>
        {
            await history.StartAsync(BeforePeriod);
            await history.StepAsync(BeforePeriod.AddHours(1), 0);
            await history.StepAsync(AfterPeriod, 1);
            await history.StepAsync(AfterPeriod.AddMinutes(1), 2);
            await history.SubmitAsync(AfterPeriod.AddMinutes(3),
                [BeforePeriod.AddHours(1), AfterPeriod, AfterPeriod.AddMinutes(1)]);
            await history.ReviewAsync(AfterPeriod.AddMinutes(4));
        });

        // Act
        var original = await beforeSubmission.Contributor.ReadAsync(Engagement(), beforeSubmission.Context,
            CancellationToken.None);
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
        Assert.Equal(Uuid.CreateVersion5(EvaluationId, "control_evaluation_v1:2"), source.VersionId);
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
        var fixture = await Fixture(AcceptedHistory);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

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
        var fixture = await Fixture(static _ => Task.CompletedTask, ControlVersionId);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

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
        var fixture = await Fixture(AcceptedHistory, Uuid.CreateVersion4());

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

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
        var fixture = await Fixture(async history =>
        {
            await history.StartAsync(BeforePeriod);
            await history.StepAsync(BeforePeriod.AddMinutes(1), 0, ControlEvaluationStates.NotTested);
            await history.StepAsync(BeforePeriod.AddMinutes(2), 1, ControlEvaluationStates.NotTested);
            await history.StepAsync(BeforePeriod.AddMinutes(3), 2, ControlEvaluationStates.NotTested);
            await history.SubmitAsync(BeforePeriod.AddMinutes(4), null, ControlEvaluationStates.NotTested);
        });

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldIgnoreOutOfScopeEvaluationGivenMalformedOtherControlHistory()
    {
        // Arrange
        var fixture = await Fixture(async history =>
        {
            await AcceptedHistory(history);
            await history.StartMalformedOtherControlAsync(BeforePeriod.AddHours(3));
        });

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
    }

    static async Task AcceptedHistory(EvaluationHistory history)
    {
        await history.StartAsync(BeforePeriod);
        await history.StepAsync(BeforePeriod.AddMinutes(1), 0);
        await history.StepAsync(BeforePeriod.AddMinutes(2), 1);
        await history.StepAsync(BeforePeriod.AddMinutes(3), 2);
        await history.SubmitAsync(BeforePeriod.AddHours(1));
        await history.ReviewAsync(BeforePeriod.AddHours(2));
    }

    static async Task<FixtureState> Fixture(Func<EvaluationHistory, Task> build, Uuid? baselineControlVersionId = null)
    {
        var store = new InMemoryEventStore();
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var history = new EvaluationHistory(store, content);
        await build(history);
        var controlSource = new TypeIBaselineSourceReference("control_version", ControlId,
            baselineControlVersionId ?? ControlVersionId, 1, new string('a', 64));
        var reader = new ControlEvaluationReader(new UnusedAggregateReader(), store, content);
        return new FixtureState(new ControlEvaluationTypeIBaselineSourceContributor(reader),
            new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), [controlSource]));
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", new DateOnly(2026, 1, 31), null,
        ["security"], Uuid.CreateVersion4(), [], "draft", 1, BeforePeriod, ReviewerId, "Examiner");

    static EvaluationProcedureStep[] ProcedureSteps() => ControlEvaluationStates.Assertions
        .Select(static assertion => new EvaluationProcedureStep(assertion, "inspection",
            [new EvaluationInspectedItem("control", ControlId.ToString(), ControlVersionId.ToString())],
            "The control meets its documented requirement.")).ToArray();

    sealed record FixtureState(ControlEvaluationTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class UnusedAggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate);
    }

    /// <summary>Raises evaluation facts on real owners, with their business times as broker commit times.</summary>
    sealed class EvaluationHistory(InMemoryEventStore store, IImmutableContentStore content)
    {
        ControlEvaluation _owner = new(TenantId, EvaluationId, EvaluationId);
        readonly ActorReference _actor = ActorReference.ForMember(EvaluatorId, "Evaluator");
        readonly Dictionary<int, EvaluationStepResultView> _results = [];

        public async Task StartAsync(DateTimeOffset at)
        {
            var steps = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.StepsSchema,
                (IReadOnlyList<EvaluationProcedureStep>)ProcedureSteps());
            var none = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.RetestDeviationsSchema,
                (IReadOnlyList<Uuid>)[]);
            Assert.True(_owner.Start(NativeFacts.Root(_owner, EvaluationId, "StartControlEvaluation", null, EvaluatorId,
                at) with
            { Actor = _actor }, ProgramId, ControlId, ControlVersionId, PlanVersionId, 1, steps, EvaluatorId, _actor, at, null,
                null, none, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
            await SaveAsync(_owner, at);
        }

        public async Task StepAsync(DateTimeOffset at, int index, string result = ControlEvaluationStates.Met)
        {
            var commandId = Uuid.CreateVersion4();
            await OpenAsync(commandId);
            var step = ProcedureSteps()[index];
            var recorded = new EvaluationStepResultView(1, result,
                result == ControlEvaluationStates.NotTested ? "No items were available." : "The control operated.",
                result == ControlEvaluationStates.NotTested ? [] : step.InspectedItems.ToArray(), _actor, at);
            _results[index] = recorded;
            var reference = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.ResultSchema,
                recorded);
            Assert.True(_owner.RecordStep(NativeFacts.Root(_owner, commandId, "RecordControlEvaluationStep",
                _owner.Revision, EvaluatorId, at), ControlEvaluationRules.StepId(EvaluationId, index), reference, null,
                NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
            await SaveAsync(_owner, at);
        }

        public async Task SubmitAsync(DateTimeOffset at, IReadOnlyList<DateTimeOffset>? recordedAt = null,
            string result = ControlEvaluationStates.Met)
        {
            var commandId = Uuid.CreateVersion4();
            await OpenAsync(commandId);
            var steps = ProcedureSteps().Select((procedure, index) => new ControlEvaluationStepView(
                ControlEvaluationRules.StepId(EvaluationId, index), index, procedure.Assertion, procedure.Method,
                procedure.InspectedItems, procedure.ExpectedCondition,
                new EvaluationStepResultView(1, result, "The control operated.",
                    result == ControlEvaluationStates.NotTested ? [] : procedure.InspectedItems, _actor,
                    recordedAt is null ? BeforePeriod.AddMinutes(index + 1) : recordedAt[index]))).ToArray();
            var conclusions = ControlEvaluationStates.Assertions.Select(static assertion =>
                new EvaluationAssertionConclusion(assertion, ControlEvaluationStates.Effective,
                    "The assertion is supported.")).ToArray();
            var submission = new EvaluationSubmissionView(1, conclusions, ControlEvaluationStates.Effective, steps, [],
                _actor, at);
            var reference = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.SubmissionSchema,
                submission);
            Assert.True(_owner.Submit(NativeFacts.Root(_owner, commandId, "SubmitControlEvaluation",
                _owner.Revision, EvaluatorId, at), reference, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
            await SaveAsync(_owner, at);
        }

        public async Task ReviewAsync(DateTimeOffset at)
        {
            var decisionId = Uuid.CreateVersion4();
            await OpenAsync(decisionId);
            var review = new ControlEvaluationReviewView(decisionId, 1, ControlEvaluationStates.Accepted,
                "The evaluation is accepted.", ReviewerId, ActorReference.ForMember(ReviewerId, "Reviewer"), at);
            var reference = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.ReviewSchema,
                review);
            Assert.True(_owner.Review(NativeFacts.Root(_owner, decisionId, "ReviewControlEvaluation", _owner.Revision,
                ReviewerId, at), ControlEvaluationStates.Accepted, reference, NativeFacts.Ref(),
                NativeFacts.Ref()).IsSuccess);
            await SaveAsync(_owner, at);
        }

        public async Task StartMalformedOtherControlAsync(DateTimeOffset at)
        {
            var evaluationId = Uuid.CreateVersion4();
            var other = new ControlEvaluation(TenantId, evaluationId);
            var steps = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.StepsSchema,
                (IReadOnlyList<EvaluationProcedureStep>)[]);
            var none = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.RetestDeviationsSchema,
                (IReadOnlyList<Uuid>)[]);
            Assert.True(other.Start(NativeFacts.Root(other, evaluationId, "StartControlEvaluation", null, EvaluatorId,
                at) with
            { Actor = _actor }, ProgramId, OtherControlId, Uuid.CreateVersion4(), PlanVersionId, 1, steps, EvaluatorId, _actor, at,
                null, null, none, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
            await SaveAsync(other, at);
        }

        async Task OpenAsync(Uuid commandId)
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in store.ReadAsync(_owner.Stream, 0, CancellationToken.None))
                facts.Add(record.Event);
            _owner = NativeFacts.Replay(new ControlEvaluation(TenantId, EvaluationId, commandId), facts);
            _ = await ControlEvaluationHistory.ReadAsync(store, _owner);
        }

        async Task SaveAsync(ControlEvaluation owner, DateTimeOffset at)
        {
            var fact = Assert.Single(new AggregateScenario<ControlEvaluation>(owner).PendingEvents);
            var committed = fact with { };
            DomainEventSeed.Attach(committed, owner.Id, (ulong)owner.Revision, occurredOn: at);
            await store.AppendAsync(owner.Stream, owner.CommittedStreamPosition, [committed]);
        }

    }
}
