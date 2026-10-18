using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class ResolveAuditorRequestHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ResolveAuditorRequest>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ResolveAuditorRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Resolving an auditor request requires a personal HTTP actor.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Resolve(aggregate.Revision, request.Resolution, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
