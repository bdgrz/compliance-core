using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class ReviewPolicyDraftHandler(PolicyOperations operations) : IRequestHandler<ReviewPolicyDraft, PolicyDecisionView>
{
    public ValueTask<Result<PolicyDecisionView>> HandleAsync(IRequestContext<ReviewPolicyDraft> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result<PolicyDecisionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy sign-off requires personal HTTP submission.")));
        var request = context.Request;
        SeparationOfDutiesWaiverProof? proof = null;
        return operations.ExecuteAsync<ReviewPolicyDraft, PolicyDecisionView>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(ReviewPolicyDraft), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                var resolved = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.PolicyId, snapshot.Revision, SeparationOfDutiesActions.Review, at, token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result.Failure(resolved.Error!);
                proof = resolved.Value;
                return Result.Success;
            },
            (rules, actor, at) => PolicyOperations.Outcome(rules.Review(request.ProgramId, request.ExpectedRevision, request.CommandId, request.Outcome, request.Rationale, actor.Reference, actor.MemberId, at, proof)), () => proof, ct);
    }
}
