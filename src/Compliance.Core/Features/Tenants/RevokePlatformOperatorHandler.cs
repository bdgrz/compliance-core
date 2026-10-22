using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RevokePlatformOperatorHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevokePlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated platform user is required.")));
        var request = context.Request;
        return executor.ExecuteAsync(new PlatformOperatorRoster(request.CommandId),
            roster => AggregateOutcome.CommitOnSuccess(roster.Revoke(request.CommandId, request.ExpectedRevision,
                actorUserId, request.UserId, request.AssignmentId, request.Reason, RbacOperationActor.From(context.Actor),
                clock.GetUtcNow(), UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId))), context, ct);
    }
}
