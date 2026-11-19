using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class ReviewBoundaryDraftHandler(BoundaryOperations operations, BoundaryImpactService impact,
    IBoundaryReferenceValidator references) : IRequestHandler<ReviewBoundaryDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundaryDraft> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Boundary decisions require personal HTTP submission.")));

        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ProgramId,
            request.CommandId, nameof(ReviewBoundaryDraft), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (snapshot.Draft is not { } draft || draft.VersionId != request.DraftVersionId ||
                    draft.Revision != request.ExpectedRevision)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The boundary draft changed. Reload it before review."));

                if (request.Outcome == "approve")
                {
                    var preview = await impact.PreviewAsync(new PreviewBoundaryImpact(request.TenantId,
                        request.ProgramId, request.DraftVersionId, request.ExpectedRevision), token)
                        .ConfigureAwait(false);
                    if (!preview.IsSuccess)
                        return Result.Failure(preview.Error!);
                    if (!preview.Value.Complete || !StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The impact preview is incomplete or changed. Reload it before approval."));
                    var referencesValid = await references.ValidateAsync(request.TenantId, request.ProgramId,
                        draft.Content, token).ConfigureAwait(false);
                    if (!referencesValid.IsSuccess)
                        return referencesValid;
                }

                var action = request.Outcome == "approve"
                    ? SeparationOfDutiesActions.Approve : SeparationOfDutiesActions.Review;
                var proof = await operations.ResolveWaiverAsync(request.TenantId,
                    request.SeparationOfDutiesWaiverId, actor.MemberId, request.ProgramId,
                    request.DraftVersionId, request.ExpectedRevision, action, at, token).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error!);
                proofs.Clear();
                if (proof.Value is { } accepted)
                    proofs.Add(accepted);
                return Result.Success;
            },
            (rules, actor, at) => BoundaryOperations.Outcome(rules.ReviewDraft(request.DraftVersionId,
                request.ExpectedRevision, request.CommandId, request.Outcome, request.Rationale,
                request.EffectiveFrom, actor.MemberId, actor.Display, at, proofs.SingleOrDefault(), request.ImpactDigest)),
            null, () => proofs, ct);
    }
}
