using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class PlaceArtifactEngagementHoldHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<PlaceArtifactEngagementHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactEngagementHold> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Engagement holds require a personal HTTP decision.")));

        var request = context.Request;
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return executor.ExecuteAsync(new ArtifactEngagementHold(request.TenantId, request.HoldId), hold =>
            AggregateOutcome.CommitOnSuccess(hold.Place(request.EngagementId, request.ReportArtifactId,
                request.Scope, request.Authority, request.Reason, actor, clock.GetUtcNow())), context, ct);
    }
}
