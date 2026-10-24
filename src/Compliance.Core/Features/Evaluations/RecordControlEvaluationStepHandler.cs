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
///     Records the evaluator's result for one frozen procedure step on that evaluation's own stream. The complete
///     result with its inspected items and the deviation it raised are immutable content, and the complete resulting
///     view is the frozen first response, so an exact retry returns the original view after later work.
/// </summary>
public sealed class RecordControlEvaluationStepHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordControlEvaluationStep, ControlEvaluationView>
{
    const string CommandKind = "RecordControlEvaluationStep";

    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<RecordControlEvaluationStep> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.EvaluationId == Uuid.Empty || request.StepId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A step result requires a command id, an evaluation, a step and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        return await EvaluationWork.RunAsync(operations, evaluations, content, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId, request.CommandId, CommandKind,
            AuthoredIntentDigest.Compute(request), request.ExpectedRevision, actor, now, async (state, token) =>
            {
                var failure = ControlEvaluationRules.CheckStep(state, request, actor.MemberId, member, now,
                    out var recorded, out var deviation);
                if (failure is not null)
                    return Result<EvaluationChange>.Failure(failure.ToRequestError());
                var result = await NativeContent.WriteAsync(content, request.TenantId,
                    ControlEvaluationContent.ResultSchema, recorded!, token).ConfigureAwait(false);
                var deviationContent = deviation is null
                    ? null
                    : await NativeContent.WriteAsync(content, request.TenantId,
                        ControlEvaluationContent.DeviationSchema, deviation, token).ConfigureAwait(false);
                var after = state.Clone();
                after.RecordStep(state.Revision + 1, request.StepId, recorded!, deviation);
                return Result<EvaluationChange>.Success(new EvaluationChange(after,
                    (ControlEvaluation owner) => owner.PrepareWork(request.ExpectedRevision),
                    (ControlEvaluation owner, OperationProvenance provenance, ImmutableContentRef first,
                        ImmutableContentRef operationResult) => owner.RecordStep(provenance, request.StepId, result,
                        deviationContent, first, operationResult)));
            }, ct).ConfigureAwait(false);
    }
}
