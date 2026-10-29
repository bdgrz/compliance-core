using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Commitments.Commands;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class ReviewCommitmentDraftHandler(CommitmentOperations operations) : IRequestHandler<ReviewCommitmentDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewCommitmentDraft> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment sign-off requires personal HTTP submission.")));
        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.DraftId, request.CommandId,
            nameof(ReviewCommitmentDraft), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.DraftId, request.DraftId, snapshot.Revision, SeparationOfDutiesActions.Review, at, token).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error!);
                proofs.Clear();
                if (proof.Value is { } allowed)
                    proofs.Add(allowed);
                return Result.Success;
            },
            (rules, actor, at) => CommitmentOperations.Outcome(rules.Review(request.ProgramId, request.ExpectedRevision, request.CommandId, request.Outcome,
                request.OwnerReference, request.Applicability, request.Interpretation, request.InterpretationNote, request.Rationale,
                actor.MemberId, actor.Display, at, proofs.SingleOrDefault(), request.SourceVerifiedReference, request.SourceEvidence)), null, () => proofs, ct);
    }
}
