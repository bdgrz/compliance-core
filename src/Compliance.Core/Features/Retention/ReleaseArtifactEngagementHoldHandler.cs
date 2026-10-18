using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ReleaseArtifactEngagementHoldHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReleaseArtifactEngagementHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleaseArtifactEngagementHold> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Engagement hold release requires a personal HTTP decision.")));

        var request = context.Request;
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return executor.ExecuteAsync(new ArtifactEngagementHold(request.TenantId, request.HoldId), hold =>
            AggregateOutcome.CommitOnSuccess(hold.Release(request.ExpectedRevision,
                request.ReleaseAuthority, request.Rationale, actor, clock.GetUtcNow())), context, ct);
    }
}
