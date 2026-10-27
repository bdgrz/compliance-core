using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ConfirmPolicyReviewHandler(PolicyOperations operations) : IRequestHandler<ConfirmPolicyReview, PolicyDecisionView>
{
    public ValueTask<Result<PolicyDecisionView>> HandleAsync(IRequestContext<ConfirmPolicyReview> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result<PolicyDecisionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy sign-off requires personal HTTP submission.")));
        var request = context.Request;
        return operations.ExecuteAsync<ConfirmPolicyReview, PolicyDecisionView>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(ConfirmPolicyReview), request.ExpectedVersion,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) => PolicyOperations.Outcome(rules.ConfirmPeriodicReview(request.ProgramId, request.ExpectedVersion, request.CommandId, request.Rationale, actor.Reference, actor.MemberId, at)), null, ct);
    }
}
