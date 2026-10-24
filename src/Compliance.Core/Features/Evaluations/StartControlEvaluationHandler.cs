using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Starts an evaluation of the control version effective today as a stream of its own. The control owner may
///     evaluate their own control (M0-D13); otherwise a program manager evaluates. A retest requires the exact
///     accepting review of its predecessor, which is only read: the predecessor is never written. The evaluation id is
///     the logical operation identity, so an exact retry returns the original view.
/// </summary>
public sealed class StartControlEvaluationHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IAggregateReader reader, ControlReader controlSources, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<StartControlEvaluation, ControlEvaluationView>
{
    const string CommandKind = "StartControlEvaluation";

    public ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<StartControlEvaluation> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlEvaluationView>> HandleCoreAsync(
        IRequestContext<StartControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.EvaluationId == Uuid.Empty || request.ControlId == Uuid.Empty ||
            request.PlanVersionId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation,
                "An evaluation requires an evaluation id, a control and an exact plan version.");

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var (current, existing) = await evaluations.ReadAsync(request.TenantId, request.EvaluationId,
            request.EvaluationId, ct).ConfigureAwait(false);
        var provenance = RootOperation.Provenance(request.EvaluationId, CommandKind, current.Stream, member, null,
            AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? steps = null;
        ImmutableContentRef? retestIds = null;
        NativeFactReference? predecessorProof = null;
        Uuid controlVersionId = default;
        Uuid planVersionId = default;
        long planVersion = 0;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (existing is not null)
                return Failure(RequestErrorKind.Conflict, "The evaluation id already identifies another evaluation.");
            var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId, request.ProgramId,
                request.ControlId, ct).ConfigureAwait(false);
            if (control is null)
                return Result<ControlEvaluationView>.Failure(ControlOperationsSource.ControlNotFound());
            if (control.EffectiveVersion(DateOnly.FromDateTime(now.UtcDateTime)) is not { } version)
                return Failure(RequestErrorKind.Conflict,
                    "The control has no approved version effective today to evaluate.");
            if (version.OwnerMemberId != actor.MemberId && !await authority.ManagesProgramAsync(request.TenantId, actor,
                    request.ProgramId, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Forbidden,
                    "Only the control owner or a program manager may evaluate this control.");
            var plan = await reader.HydrateAsync(new ControlEvaluationPlan(request.TenantId, request.ProgramId,
                request.ControlId), ct).ConfigureAwait(false);
            var planned = plan.FindVersion(request.PlanVersionId);
            if (planned is null)
                return Failure(RequestErrorKind.NotFound, "The control evaluation plan version was not found.");
            if (planned.ControlVersionId != version.VersionId)
                return Failure(RequestErrorKind.Conflict, "The evaluation plan is bound to a different control version.");
            if (request.RetestOfEvaluationId is null && planned.Version != plan.Revision)
                return Failure(RequestErrorKind.Conflict,
                    "A new evaluation must use the current control evaluation plan version.");
            if (planned.TesterIndependenceRequired && version.OwnerMemberId == actor.MemberId)
                return Failure(RequestErrorKind.Forbidden,
                    "This evaluation plan requires an evaluator independent of the control owner.");
            if (planned.Version < 1)
                return Failure(RequestErrorKind.Validation,
                    "An evaluation must use an exact control-evaluation plan version.");

            IReadOnlyList<Uuid> retested = [];
            if (request.RetestOfEvaluationId is { } predecessorId)
            {
                var (predecessorOwner, predecessor) = await evaluations.ReadAsync(request.TenantId, predecessorId,
                    default, ct).ConfigureAwait(false);
                var failure = predecessor is null || !predecessorOwner.Belongs(request.ProgramId, request.ControlId)
                    ? CommandFailure.MissingRecord("The evaluation to retest was not found.")
                    : ControlEvaluationRules.CheckRetest(predecessor, request.ControlId, planned.Version);
                if (failure is not null)
                    return Result<ControlEvaluationView>.Failure(failure.ToRequestError());
                var accepted = predecessor!.AcceptedSubmission!;
                retested = accepted.Deviations.Where(static deviation =>
                    deviation.Classification == ControlEvaluationStates.Material)
                    .Select(static deviation => deviation.DeviationId).ToArray();
                predecessorProof = new NativeFactReference(predecessorOwner.Stream, predecessor.AcceptingFact!.Value.EventId,
                    predecessor.AcceptingFact.Value.AggregateVersion);
            }

            var procedure = ControlEvaluationRules.Clean(planned.Steps);
            if (ControlEvaluationRules.ValidateProcedure(procedure) is { } invalid)
                return Result<ControlEvaluationView>.Failure(invalid.ToRequestError());
            steps = await NativeContent.WriteAsync(content, request.TenantId, ControlEvaluationContent.StepsSchema,
                procedure, ct).ConfigureAwait(false);
            retestIds = await NativeContent.WriteAsync(content, request.TenantId,
                ControlEvaluationContent.RetestDeviationsSchema, retested, ct).ConfigureAwait(false);
            (controlVersionId, planVersionId, planVersion) = (version.VersionId, planned.PlanVersionId, planned.Version);
            var started = new ControlEvaluationState(request.TenantId, request.ProgramId, request.ControlId,
                request.EvaluationId, controlVersionId, planVersionId, planVersion, procedure, actor.MemberId, member,
                now, request.RetestOfEvaluationId, retested, predecessorProof, 1);
            (response, result) = await NativeContent.FreezeAsync(content, request.TenantId,
                ControlEvaluationContent.ViewSchema, started.ToView([]), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlEvaluation>(binding,
            () => new ControlEvaluation(request.TenantId, request.EvaluationId, request.EvaluationId),
            owner => owner.Acceptance, owner => Prepare(owner, response),
            (ControlEvaluation owner, ImmutableContentRef frozen) => owner.Start(provenance, request.ProgramId,
                request.ControlId, controlVersionId, planVersionId, planVersion, steps!, actor.MemberId, member, now,
                request.RetestOfEvaluationId, predecessorProof, retestIds!, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlEvaluationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlEvaluation owner, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareStart();
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evaluation changed while the start was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<ControlEvaluationView> Failure(RequestErrorKind kind, string message) =>
        Result<ControlEvaluationView>.Failure(new RequestError(kind, message));
}
