using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class GrantPlatformOperatorHandler(IAggregateExecutor executor, IAggregateReader reader,
    IPlatformUserDirectoryReader users, TimeProvider clock) : IRequestHandler<GrantPlatformOperator>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated platform user is required."));
        var request = context.Request;
        if (request.UserId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A platform user is required."));
        var observed = await reader.HydrateAsync(new PlatformOperatorRoster(request.CommandId), ct).ConfigureAwait(false);
        if (!observed.HasSelectedOperation && !await users.ExistsAsync(request.UserId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The platform user is not available yet.", isTransient: true));
        return await executor.ExecuteAsync(new PlatformOperatorRoster(request.CommandId),
            roster => AggregateOutcome.CommitOnSuccess(roster.Grant(request.CommandId, request.ExpectedRevision,
                actorUserId, request.UserId, request.Reason, RbacOperationActor.From(context.Actor), clock.GetUtcNow(),
                UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId))), context, ct).ConfigureAwait(false);
    }
}
