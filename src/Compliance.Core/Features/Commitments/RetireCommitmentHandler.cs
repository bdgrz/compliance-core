using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class RetireCommitmentHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireCommitment>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireCommitment> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment retirement requires personal HTTP submission.")));

        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.CommitmentId),
            draft => CommandFailureRequestAdapter.ToOutcome(draft.Retire(request.ProgramId,
                request.ExpectedRevision, request.EffectiveOn, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
