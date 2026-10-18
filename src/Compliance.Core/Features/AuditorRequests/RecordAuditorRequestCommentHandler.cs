using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class RecordAuditorRequestCommentHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordAuditorRequestComment>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditorRequestComment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Adding to auditor request discussion requires a personal HTTP actor.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.RecordComment(aggregate.Revision, request.Body, request.Compartment,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
