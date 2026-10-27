using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

/// <summary>
///     Records the evaluator's personal sign-off of separate assertion conclusions; HTTP-only. Every step needs a
///     result and every minor deviation a disposition before the round can be submitted. The complete submission is
///     immutable content, and an exact retry returns the original view even after the review.
/// </summary>
public sealed class SubmitControlEvaluationHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SubmitControlEvaluation, ControlEvaluationView>
{
    const string CommandKind = "SubmitControlEvaluation";

    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<SubmitControlEvaluation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control evaluation sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.EvaluationId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A submission requires a command id, an evaluation and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        return await EvaluationWork.RunAsync(operations, evaluations, content, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId, request.CommandId, CommandKind,
            AuthoredIntentDigest.Compute(request), request.ExpectedRevision, actor, now, async (state, token) =>
            {
                var failure = ControlEvaluationRules.CheckSubmit(state, request, actor.MemberId, member, now,
                    out var submission);
                if (failure is not null)
                    return Result<EvaluationChange>.Failure(failure.ToRequestError());
                var submitted = await NativeContent.WriteAsync(content, request.TenantId,
                    ControlEvaluationContent.SubmissionSchema, submission!, token).ConfigureAwait(false);
                var after = state.Clone();
                after.Submit(state.Revision + 1, submission!);
                return Result<EvaluationChange>.Success(new EvaluationChange(after,
                    (ControlEvaluation owner) => owner.PrepareWork(request.ExpectedRevision),
                    (ControlEvaluation owner, OperationProvenance provenance, ImmutableContentRef first,
                        ImmutableContentRef operationResult) => owner.Submit(provenance, submitted, first,
                        operationResult)));
            }, ct).ConfigureAwait(false);
    }
}
