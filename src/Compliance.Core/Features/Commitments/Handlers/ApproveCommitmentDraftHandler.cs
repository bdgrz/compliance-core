using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
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
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Queries;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class ApproveCommitmentDraftHandler(CommitmentOperations operations, CommitmentImpactService impact) : IRequestHandler<ApproveCommitmentDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveCommitmentDraft> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment sign-off requires personal HTTP submission.")));
        var request = context.Request;
        var proofs = new List<SeparationOfDutiesWaiverProof>();
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.DraftId, request.CommandId,
            nameof(ApproveCommitmentDraft), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                var preview = await impact.PreviewAsync(new PreviewCommitmentImpact(request.TenantId, request.ProgramId,
                    request.DraftId, request.ExpectedRevision), token).ConfigureAwait(false);
                if (!preview.IsSuccess)
                    return Result.Failure(preview.Error!);
                if (!preview.Value.Complete)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The impact preview is incomplete. Approval cannot acknowledge it."));
                if (!StringComparer.Ordinal.Equals(request.ImpactDigest, preview.Value.Digest))
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The impact preview changed. Reload it before approval."));
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.DraftId, request.DraftId, snapshot.Revision, SeparationOfDutiesActions.Approve, at, token).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error!);
                proofs.Clear();
                if (proof.Value is { } allowed)
                    proofs.Add(allowed);
                return Result.Success;
            },
            (rules, actor, at) => CommitmentOperations.Outcome(rules.Approve(request.ProgramId, request.ExpectedRevision, request.CommandId, request.AcceptedReviewDecisionId,
                request.EffectiveFrom, request.Rationale, request.ImpactDigest, actor.MemberId, actor.Display, at, proofs.SingleOrDefault())), null, () => proofs, ct);
    }
}
