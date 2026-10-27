using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
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
///     Records the independent reviewer's decision; HTTP-only. The reviewer must manage the program (Compliance Lead
///     or Org Admin) and must not be the evaluator unless an approved, active waiver scoped to this evaluation round
///     applies. The decision is checked against the submitted round's exact revision, and an exact retry returns the
///     original view even after the evaluation moved on.
/// </summary>
public sealed class ReviewControlEvaluationHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IAggregateReader reader, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewControlEvaluation, ControlEvaluationView>
{
    const string CommandKind = "ReviewControlEvaluation";

    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<ReviewControlEvaluation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control evaluation sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.EvaluationId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A review requires a command id, an evaluation and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (!await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a Compliance Lead or Org Admin for the program may review an evaluation."));
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        return await EvaluationWork.RunAsync(operations, evaluations, content, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId, request.CommandId, CommandKind,
            AuthoredIntentDigest.Compute(request), request.ExpectedRevision, actor, now, async (state, token) =>
            {
                SeparationOfDutiesWaiver? waiver = null;
                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                        .ConfigureAwait(false);
                var failure = ControlEvaluationRules.CheckReview(state, request, actor.MemberId, member, now, waiver,
                    out var review);
                if (failure is not null)
                    return Result<EvaluationChange>.Failure(failure.ToRequestError());
                var reviewContent = await NativeContent.WriteAsync(content, request.TenantId,
                    ControlEvaluationContent.ReviewSchema, review!, token).ConfigureAwait(false);
                var after = state.Clone();
                after.Review(state.Revision + 1, review!);
                return Result<EvaluationChange>.Success(new EvaluationChange(after,
                    (ControlEvaluation owner) => owner.PrepareReview(request.ExpectedRevision),
                    (ControlEvaluation owner, OperationProvenance provenance, ImmutableContentRef first,
                        ImmutableContentRef operationResult) => owner.Review(provenance, request.Decision,
                        reviewContent, first, operationResult)));
            }, ct).ConfigureAwait(false);
    }
}
