using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FitzControlEvaluationWorkItemDirectoryTests
{
    readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));

    [Fact]
    public async Task ShouldRejectSkippedEvaluationRevisionGivenProjectedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var evaluationId = Uuid.CreateVersion4();
        var evaluatorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(evaluatorId, "Evaluator");
        var startedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var procedures = Procedures();
        var directory = new FitzControlEvaluationWorkItemDirectory(new InMemoryKvClient(), _content);
        var identity = new CheckpointIdentity(FitzControlEvaluationWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-evaluations"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(await StartedAsync(tenantId, programId, controlId, evaluationId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), evaluatorId, actor, procedures, startedAt));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(await StepAsync(tenantId, programId, controlId, evaluationId, 3, 0,
                procedures, actor, 1, startedAt.AddMinutes(1))));

        // Assert
        Assert.Contains("advance its existing source revision", failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldProjectOneReviewPerPendingRoundGivenEvaluationLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var evaluationId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var planVersionId = Uuid.CreateVersion4();
        var evaluatorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(evaluatorId, "Evaluator");
        var reviewer = ActorReference.ForMember(reviewerId, "Reviewer");
        var startedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var procedures = Procedures();
        var directory = new FitzControlEvaluationWorkItemDirectory(new InMemoryKvClient(), _content);
        var identity = new CheckpointIdentity(FitzControlEvaluationWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-evaluations"));
        var firstCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-round-one"));
        var secondCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-round-two"));
        var finalCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-accepted"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(await StartedAsync(tenantId, programId, controlId, evaluationId,
                controlVersionId, planVersionId, evaluatorId, actor, procedures, startedAt));
            await ApplyStepResultsAsync(directory, tenantId, programId, controlId, evaluationId,
                procedures, actor, 2, 1, startedAt.AddMinutes(1));
            await directory.ApplyAsync(await SubmittedAsync(tenantId, programId, controlId, evaluationId,
                procedures, actor, 5, 1, startedAt.AddMinutes(2)));
            await batch.CommitAsync(firstCheckpoint);
        }
        var firstRound = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         firstCheckpoint)))
        {
            await directory.ApplyAsync(await ReviewedAsync(tenantId, programId, controlId, evaluationId,
                reviewerId, reviewer, 6, 1, "changes_requested", startedAt.AddMinutes(3)));
            await ApplyStepResultsAsync(directory, tenantId, programId, controlId, evaluationId,
                procedures, actor, 7, 2, startedAt.AddMinutes(4));
            await directory.ApplyAsync(await SubmittedAsync(tenantId, programId, controlId, evaluationId,
                procedures, actor, 10, 2, startedAt.AddMinutes(5)));
            await batch.CommitAsync(secondCheckpoint);
        }
        var secondRound = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         secondCheckpoint)))
        {
            await directory.ApplyAsync(await ReviewedAsync(tenantId, programId, controlId, evaluationId,
                reviewerId, reviewer, 11, 2, ControlEvaluationStates.Accepted,
                startedAt.AddMinutes(6)));
            await batch.CommitAsync(finalCheckpoint);
        }
        var accepted = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(firstRound.IsSuccess);
        var first = Assert.Single(firstRound.Value);
        Assert.Equal(WorkSource.ControlEvaluationReview, first.Kind);
        Assert.Equal(evaluationId, first.SourceId);
        Assert.Equal(controlId, first.ControlId);
        Assert.Equal("Review control evaluation", first.Summary);
        Assert.Equal("Control evaluation round 1 is awaiting independent review.", first.Reason);
        Assert.Equal("review", first.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}/" +
            $"evaluations/{evaluationId}/reviews", first.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            first.Responsible);
        Assert.Contains(evaluatorId, first.Excluded);
        Assert.Equal(startedAt.AddMinutes(2), first.CreatedAt);
        Assert.Equal(WorkCandidate.IdFor(Uuid.CreateVersion5(evaluationId,
            "control-evaluation-review\n1"), WorkSource.ControlEvaluationReview), first.WorkItemId);

        Assert.True(secondRound.IsSuccess);
        var second = Assert.Single(secondRound.Value);
        Assert.Equal(WorkSource.ControlEvaluationReview, second.Kind);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Contains(evaluatorId, second.Excluded);
        Assert.Empty(accepted.Value);
        Assert.Equal(11, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(finalCheckpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    static OperationProvenance Root(Uuid tenantId, Uuid evaluationId, Uuid logicalId, long? expected, Uuid actorId,
        DateTimeOffset at) => NativeFacts.Root(new ControlEvaluation(tenantId, evaluationId), logicalId, "Test",
        expected, actorId, at);

    async Task<ControlEvaluationStarted> StartedAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid controlVersionId, Uuid planVersionId, Uuid evaluatorId, ActorReference actor,
        IReadOnlyList<EvaluationProcedureStep> procedures, DateTimeOffset startedAt)
    {
        var steps = await NativeContent.WriteAsync(_content, tenantId, ControlEvaluationContent.StepsSchema, procedures);
        var none = await NativeContent.WriteAsync(_content, tenantId, ControlEvaluationContent.RetestDeviationsSchema,
            (IReadOnlyList<Uuid>)[]);
        return new ControlEvaluationStarted(tenantId, programId, controlId, evaluationId, 1, controlVersionId,
            planVersionId, 1, steps, evaluatorId, actor, startedAt, null, null, none,
            Root(tenantId, evaluationId, evaluationId, null, evaluatorId, startedAt), NativeFacts.Ref(),
            NativeFacts.Ref());
    }

    async Task<ControlEvaluationStepRecorded> StepAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, long revision, int index, IReadOnlyList<EvaluationProcedureStep> procedures,
        ActorReference actor, int round, DateTimeOffset recordedAt)
    {
        var result = await NativeContent.WriteAsync(_content, tenantId, ControlEvaluationContent.ResultSchema,
            new EvaluationStepResultView(round, ControlEvaluationStates.Met, "Inspected the exact items.",
                procedures[index].InspectedItems, actor, recordedAt));
        return new ControlEvaluationStepRecorded(tenantId, programId, controlId, evaluationId, revision,
            ControlEvaluationRules.StepId(evaluationId, index), result, null,
            Root(tenantId, evaluationId, Uuid.CreateVersion4(), revision - 1,
                Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), recordedAt),
            NativeFacts.Ref(), NativeFacts.Ref());
    }

    async Task ApplyStepResultsAsync(FitzControlEvaluationWorkItemDirectory directory,
        Uuid tenantId, Uuid programId, Uuid controlId, Uuid evaluationId,
        IReadOnlyList<EvaluationProcedureStep> procedures, ActorReference actor, long firstRevision,
        int round, DateTimeOffset recordedAt)
    {
        for (var index = 0; index < procedures.Count; index++)
            await directory.ApplyAsync(await StepAsync(tenantId, programId, controlId, evaluationId,
                firstRevision + index, index, procedures, actor, round, recordedAt.AddMinutes(index)));
    }

    async Task<ControlEvaluationSubmitted> SubmittedAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, IReadOnlyList<EvaluationProcedureStep> procedures, ActorReference actor,
        long revision, int round, DateTimeOffset submittedAt)
    {
        var steps = procedures.Select((procedure, index) => new ControlEvaluationStepView(
            ControlEvaluationRules.StepId(evaluationId, index), index, procedure.Assertion,
            procedure.Method, procedure.InspectedItems, procedure.ExpectedCondition,
            new EvaluationStepResultView(round, ControlEvaluationStates.Met,
                "Inspected the exact items.", procedure.InspectedItems, actor,
                submittedAt.AddMinutes(-1)))).ToArray();
        var submission = await NativeContent.WriteAsync(_content, tenantId, ControlEvaluationContent.SubmissionSchema,
            new EvaluationSubmissionView(round,
                [new("design", ControlEvaluationStates.Effective, "Design is effective."),
                    new("implementation", ControlEvaluationStates.Effective, "Implementation is effective."),
                    new("evidence_sufficiency", ControlEvaluationStates.Effective, "Evidence is sufficient.")],
                ControlEvaluationStates.Effective, steps, [], actor, submittedAt));
        return new ControlEvaluationSubmitted(tenantId, programId, controlId, evaluationId, revision, submission,
            Root(tenantId, evaluationId, Uuid.CreateVersion4(), revision - 1,
                Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), submittedAt),
            NativeFacts.Ref(), NativeFacts.Ref());
    }

    async Task<ControlEvaluationReviewed> ReviewedAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid reviewerId, ActorReference reviewer, long revision, int round, string decision,
        DateTimeOffset reviewedAt)
    {
        var decisionId = Uuid.CreateVersion4();
        var review = await NativeContent.WriteAsync(_content, tenantId, ControlEvaluationContent.ReviewSchema,
            new ControlEvaluationReviewView(decisionId, round, decision, "Reviewed the submitted evaluation.",
                reviewerId, reviewer, reviewedAt));
        return new ControlEvaluationReviewed(tenantId, programId, controlId, evaluationId, revision, decision, review,
            Root(tenantId, evaluationId, decisionId, revision - 1, reviewerId, reviewedAt), NativeFacts.Ref(),
            NativeFacts.Ref());
    }

    static IReadOnlyList<EvaluationProcedureStep> Procedures() =>
    [
        new("design", "inspection", [new("record", "policy/access-review", "v1")],
            "The policy requires an access review."),
        new("implementation", "reperformance", [new("artifact", "exports/q3.csv", "sha256:ab12")],
            "The export contains every active account."),
        new("evidence_sufficiency", "inspection", [new("artifact", "exports/q3.csv", "sha256:ab12")],
            "The export is complete and dated."),
    ];
}
