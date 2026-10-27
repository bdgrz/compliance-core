using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class ApproveBoundaryHandler(BoundaryOperations operations, BoundaryImpactService impact, IBoundaryReferenceValidator references)
    : IRequestHandler<ApproveBoundary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveBoundary> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Boundary decision requires personal HTTP submission.")));
        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        return operations.ExecuteUnitAsync(context, request.TenantId, null, request.BoundaryId, request.CommandId,
            nameof(ApproveBoundary), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                var preview = await impact.PreviewAsync(new PreviewBoundaryImpact(request.TenantId, request.BoundaryId,
                    request.DraftVersionId, request.ExpectedRevision), token).ConfigureAwait(false);
                if (!preview.IsSuccess)
                    return Result.Failure(preview.Error!);
                if (!preview.Value.Complete)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The impact preview is incomplete. Pending or incomplete contexts: " + string.Join(", ", preview.Value.PendingContexts)));
                if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The impact preview changed. Reload it before approval."));
                if (snapshot.Draft is not { } draft || draft.VersionId != request.DraftVersionId || draft.Revision != request.ExpectedRevision)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The boundary draft changed. Reload it before approval."));
                var valid = await references.ValidateAsync(request.TenantId, snapshot.ProgramId, draft.Content, token).ConfigureAwait(false);
                if (!valid.IsSuccess)
                    return valid;
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.BoundaryId, request.DraftVersionId, request.ExpectedRevision, SeparationOfDutiesActions.Approve, at, token).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error!);
                proofs.Clear();
                if (proof.Value is { } accepted)
                    proofs.Add(accepted);
                return Result.Success;
            },
            (rules, actor, at) => BoundaryOperations.Outcome(rules.Approve(request.DraftVersionId, request.ExpectedRevision,
                request.CommandId, request.AcceptedReviewDecisionId, request.EffectiveFrom, request.Rationale, request.ImpactDigest,
                actor.MemberId, actor.Display, at, proofs.SingleOrDefault())), null, () => proofs, ct);
    }
}
