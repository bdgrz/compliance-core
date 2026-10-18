using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class ReopenAuditorRequestHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReopenAuditorRequest>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReopenAuditorRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Reopening an auditor request requires a personal HTTP actor.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Reopen(aggregate.Revision, request.ReopenReason, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
