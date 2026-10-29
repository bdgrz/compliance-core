using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Boundaries.Commands;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class ReviewBoundaryHandler(BoundaryOperations operations) : IRequestHandler<ReviewBoundary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewBoundary> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Boundary decision requires personal HTTP submission.")));
        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        return operations.ExecuteUnitAsync(context, request.TenantId, null, request.BoundaryId, request.CommandId,
            nameof(ReviewBoundary), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (snapshot.DraftVersionId != request.DraftVersionId || snapshot.DraftRevision != request.ExpectedRevision)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The boundary draft changed. Reload it before review."));
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.BoundaryId, request.DraftVersionId, request.ExpectedRevision, SeparationOfDutiesActions.Review, at, token).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error!);
                proofs.Clear();
                if (proof.Value is { } accepted)
                    proofs.Add(accepted);
                return Result.Success;
            },
            (rules, actor, at) => BoundaryOperations.Outcome(rules.Review(request.DraftVersionId, request.ExpectedRevision,
                request.CommandId, request.Outcome, request.Rationale, actor.MemberId, actor.Display, at, proofs.SingleOrDefault())),
            null, () => proofs, ct);
    }
}
