using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class RetireCommitmentHandler(CommitmentOperations operations) : IRequestHandler<RetireCommitment>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireCommitment> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment retirement requires personal HTTP submission.")));
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.CommitmentId, request.CommandId,
            nameof(RetireCommitment), request.ExpectedRevision, (_, _, _, _) => CommitmentOperations.Ready(),
            (rules, actor, at) => CommitmentOperations.Outcome(rules.Retire(request.ProgramId, request.ExpectedRevision,
                request.EffectiveOn, request.Rationale, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
