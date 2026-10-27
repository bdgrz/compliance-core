using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds readiness as real native facts on their owners, with the frozen intents, content and first responses the
///     handlers would write, but without any authorization, source read or process. A seeded assessment is admitted,
///     computed and adopted; a seeded decision is accepted and recorded in one step, as the decision process leaves it.
/// </summary>
static class ReadinessSeed
{
    public static ReadinessEvaluation Evaluation(Uuid programId, DateTimeOffset asOf) =>
        ReadinessRules.Evaluate(programId, asOf, null, [], [], new Dictionary<Uuid, Bdgrz.Compliance.Features.Controls.Models.ControlVersionView?>());

    /// <summary>Admits, computes and adopts an assessment; returns the program readiness revision afterwards.</summary>
    public static async Task<long> AdoptAsync(IAggregateReader reader, IAggregateExecutor executor,
        IImmutableContentStore content, Uuid tenantId, Uuid programId, Uuid assessmentId,
        ReadinessEvaluation evaluation, DateTimeOffset asOf, Uuid runnerMemberId, DateTimeOffset at,
        string ruleVersion = ReadinessRules.Version)
    {
        var context = new RequestDispatchContext(RequestActor.System);
        var actor = ActorReference.ForMember(runnerMemberId, "Readiness runner");
        var current = await reader.HydrateAsync(new ProgramReadiness(tenantId, programId, assessmentId));
        var provenance = ReadinessContent.Provenance(assessmentId, "RunReadinessAssessment", current.Stream, actor,
            (ulong)current.Revision, new string('a', 64), at);
        var inputs = await ReadinessContent.WriteAsync(content, tenantId, ReadinessInputSnapshot.Schema,
            new ReadinessInputSnapshot(programId, asOf, null, null, await Placeholder(content, tenantId),
                await Placeholder(content, tenantId), await Placeholder(content, tenantId),
                await Placeholder(content, tenantId), await ImmutableSelectionManifests.WriteAsync<AuthoritativeReadCut>(
                    content, tenantId.ToString(), Uuid.CreateVersion4(), "authoritative-read-cut.v1", [],
                    SelectionOrder.Authored)), CancellationToken.None);
        var intent = await ReadinessContent.WriteAsync(content, tenantId, ReadinessRunIntent.Schema,
            new ReadinessRunIntent(ReadinessContent.Header(provenance, at), programId, assessmentId,
                (ulong)current.Revision, (ulong)current.AdoptionGeneration + 1, ReadinessRules.Version, asOf, inputs,
                runnerMemberId), CancellationToken.None);
        var ack = await ReadinessContent.AcceptedResponseAsync(content, tenantId, assessmentId, "/operations/{operation_id}",
            at, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), ack);
        var requested = await executor.ExecuteAsync(new ProgramReadiness(tenantId, programId, assessmentId),
            (ProgramReadiness readiness) => AggregateOutcome.Commit(readiness.RequestRun(provenance, intent, first)),
            context, CancellationToken.None);
        Assert.True(requested.IsSuccess, requested.Error?.Message);

        var inputsReference = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.InputsSchema,
            evaluation.Inputs, CancellationToken.None);
        var findings = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.FindingsSchema,
            evaluation.Findings, CancellationToken.None);
        var gaps = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.GapsSchema, evaluation.Gaps,
            CancellationToken.None);
        var scopes = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.AccessReviewScopesSchema,
            evaluation.AccessReviewScopes, CancellationToken.None);
        var assessment = new ReadinessAssessment(tenantId, programId, assessmentId, assessmentId);
        var compute = Child(Uuid.CreateVersion4(), "ComputeReadinessAssessment", provenance, assessment.Stream, at);
        var recorded = await executor.ExecuteAsync(new ReadinessAssessment(tenantId, programId, assessmentId),
            (ReadinessAssessment item) => AggregateOutcome.Commit(item.Record(compute, ruleVersion, asOf, null,
                evaluation.InputFingerprint, inputsReference, findings, gaps, scopes, runnerMemberId, actor, at, intent)),
            context, CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);

        var program = await reader.HydrateAsync(new ProgramReadiness(tenantId, programId));
        var adopt = Child(Uuid.CreateVersion4(), "AdoptReadinessAssessment", provenance, program.Stream, at);
        var proofs = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.AdoptionProofsSchema,
            (IReadOnlyList<NativeFactReference>)[], CancellationToken.None);
        var result = await ReadinessContent.WriteResultAsync(content, tenantId, ReadinessContent.RegistrationSchema,
            await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.RegistrationSchema,
                new ReadinessAssessmentRegistration(assessmentId, program.Revision + 1), CancellationToken.None),
            CancellationToken.None);
        var adopted = await executor.ExecuteAsync(new ProgramReadiness(tenantId, programId, adopt.LogicalId, assessmentId),
            (ProgramReadiness readiness) => AggregateOutcome.Commit(readiness.Adopt(adopt, intent, program.AdoptionGeneration,
                proofs, result)), context, CancellationToken.None);
        Assert.True(adopted.IsSuccess, adopted.Error?.Message);
        return program.Revision + 1;
    }

    /// <summary>Records a gap plan as the root fact of its operation at the current plan revision.</summary>
    public static async Task PlanAsync(IAggregateReader reader, IAggregateExecutor executor, IImmutableContentStore content,
        Uuid tenantId, Uuid programId, Uuid gapId, Uuid ownerMemberId, DateOnly targetDate, string action,
        Uuid actorMemberId, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        var current = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, programId, gapId, commandId));
        var actor = ActorReference.ForMember(actorMemberId, "Planner");
        var view = new ReadinessGapPlanView(gapId, ownerMemberId, targetDate, action, actor, at)
        {
            Revision = current.Revision + 1,
        };
        var planContent = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.PlanSchema, view,
            CancellationToken.None);
        var (response, result) = await ReadinessContent.FreezeAsync(content, tenantId, ReadinessContent.PlanViewSchema,
            view, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response);
        var provenance = ReadinessContent.Provenance(commandId, "PlanReadinessGap", current.Stream, actor,
            (ulong)current.Revision, new string('b', 64), at);
        var planned = await executor.ExecuteAsync(new ReadinessGapPlan(tenantId, programId, gapId, commandId),
            (ReadinessGapPlan plan) => AggregateOutcome.Commit(plan.Plan(provenance, planContent, first, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(planned.IsSuccess, planned.Error?.Message);
    }

    /// <summary>
    ///     Accepts and records a decision on the latest adopted assessment in one step. The frozen manifests are empty
    ///     and no gap plan is held; the recorded views carry the gaps and plans the caller states.
    /// </summary>
    public static async Task<Uuid> DecideAsync(IAggregateReader reader, IAggregateExecutor executor,
        IImmutableContentStore content, Uuid tenantId, Uuid programId, Uuid assessmentId, ReadinessDecisionKind kind,
        string outcome, string rationale, Uuid deciderMemberId, DateTimeOffset at,
        IReadOnlyList<TypeIEntryUnresolvedItemView>? unresolved = null, string ruleVersion = "readiness-rules/13")
    {
        var decisionId = Uuid.CreateVersion4();
        var context = new RequestDispatchContext(RequestActor.System);
        var actor = ActorReference.ForMember(deciderMemberId, "Decider");
        var current = await reader.HydrateAsync(new ProgramReadiness(tenantId, programId, decisionId));
        var provenance = ReadinessContent.Provenance(decisionId, kind == ReadinessDecisionKind.Readiness
                ? "DecideReadiness" : "DecideTypeIEntry", current.Stream, actor, (ulong)current.Revision,
            new string('c', 64), at);
        var empty = await ImmutableSelectionManifests.WriteAsync<ReadinessGapView>(content, tenantId.ToString(),
            Uuid.CreateVersion4(), ReadinessContent.GapItemSchema, [], SelectionOrder.CanonicalKey);
        var none = await ImmutableSelectionManifests.WriteAsync<Uuid>(content, tenantId.ToString(), Uuid.CreateVersion4(),
            ReadinessContent.AcknowledgedGapSchema, [], SelectionOrder.CanonicalKey);
        var uses = await ImmutableSelectionManifests.WriteAsync<SourceUseRequirement>(content, tenantId.ToString(),
            Uuid.CreateVersion4(), SourceUseRequirement.Schema, [], SelectionOrder.CanonicalKey);
        var locator = new SourceFactLocator(new ReadinessAssessment(tenantId, programId, assessmentId).Stream,
            Uuid.CreateVersion4(), 0, 0, 0, 1, new string('d', 64));
        var intent = await ReadinessContent.WriteAsync(content, tenantId, ReadinessDecisionIntent.Schema,
            new ReadinessDecisionIntent(ReadinessContent.Header(provenance, at), programId, assessmentId, locator, kind,
                outcome, rationale, ruleVersion, empty, none, uses, null), CancellationToken.None);
        var ack = await ReadinessContent.AcceptedResponseAsync(content, tenantId, decisionId, "/operations/{operation_id}",
            at, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), ack);
        var accepted = await executor.ExecuteAsync(new ProgramReadiness(tenantId, programId, decisionId),
            (ProgramReadiness readiness) => AggregateOutcome.Commit(readiness.AcceptDecision(kind, assessmentId, provenance,
                intent, first)), context, CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);

        var program = await reader.HydrateAsync(new ProgramReadiness(tenantId, programId, decisionId));
        var recorded = Child(Uuid.CreateVersion4(), kind == ReadinessDecisionKind.Readiness
            ? "CompleteReadinessDecision" : "CompleteTypeIEntryDecision", provenance, program.Stream, at);
        var proofs = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.DecisionProofsSchema,
            (IReadOnlyList<ReadinessPlanProof>)[], CancellationToken.None);
        ImmutableContentRef body;
        string resultSchema;
        if (kind == ReadinessDecisionKind.Readiness)
        {
            resultSchema = ReadinessContent.ReadinessDecisionViewSchema;
            body = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.ReadinessDecisionSchema,
                new ReadinessDecisionView(decisionId, assessmentId, outcome, rationale, deciderMemberId, actor, at, null),
                CancellationToken.None);
        }
        else
        {
            resultSchema = ReadinessContent.TypeIEntryDecisionViewSchema;
            body = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.TypeIEntryDecisionSchema,
                new TypeIEntryDecisionView(decisionId, assessmentId, ruleVersion, at, new string('a', 64), outcome,
                    rationale, unresolved ?? [], deciderMemberId, actor, at, null), CancellationToken.None);
        }

        var result = await ReadinessContent.WriteResultAsync(content, tenantId, resultSchema, body, CancellationToken.None);
        var done = await executor.ExecuteAsync(new ProgramReadiness(tenantId, programId, recorded.LogicalId, decisionId),
            (ProgramReadiness readiness) => AggregateOutcome.Commit(kind == ReadinessDecisionKind.Readiness
                ? readiness.RecordReadinessDecision(recorded, body, assessmentId, proofs, intent, result)
                : readiness.RecordTypeIEntryDecision(recorded, body, assessmentId, proofs, intent, result)),
            context, CancellationToken.None);
        Assert.True(done.IsSuccess, done.Error?.Message);
        return decisionId;
    }

    /// <summary>Records an attributed annotation on the independent gap plan owner.</summary>
    public static async Task<ReadinessAnnotationView> AnnotateAsync(IAggregateReader reader, IAggregateExecutor executor,
        IImmutableContentStore content, Uuid tenantId, Uuid programId, Uuid assessmentId, Uuid gapId,
        string body, Uuid memberId, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, programId, gapId, commandId));
        var actor = ActorReference.ForMember(memberId, "Annotation author");
        var view = new ReadinessAnnotationView(commandId, assessmentId, gapId, body, memberId, actor, at);
        var annotation = await ReadinessContent.WriteAsync(content, tenantId, ReadinessContent.AnnotationSchema, view,
            CancellationToken.None);
        var (response, result) = await ReadinessContent.FreezeAsync(content, tenantId,
            ReadinessContent.AnnotationViewSchema, view, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response);
        var operation = ReadinessContent.Provenance(commandId, "AnnotateReadinessGap", owner.Stream, actor,
            (ulong)owner.Revision, new string('e', 64), at);
        var recorded = await executor.ExecuteAsync(new ReadinessGapPlan(tenantId, programId, gapId, commandId),
            (ReadinessGapPlan plan) => AggregateOutcome.Commit(plan.Annotate(operation, annotation, first, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return view;
    }

    static OperationProvenance Child(Uuid commandId, string kind, OperationProvenance root, EventStreamAddress owner,
        DateTimeOffset at) => new(commandId, root.RootOperationId, null, kind, root.Actor, owner, root.Episode,
        root.OriginalExpectedRevision, root.AuthoredIntentSha256, at, owner, owner);

    static Task<ImmutableContentRef> Placeholder(IImmutableContentStore content, Uuid tenantId) =>
        ReadinessContent.WriteAsync(content, tenantId, "readiness-input-snapshot.placeholder.v1",
            (IReadOnlyList<Uuid>)[], CancellationToken.None);
}
