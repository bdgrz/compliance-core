using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed class ReviseAuditorRequestHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseAuditorRequest>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditorRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising an auditor request requires a personal HTTP actor.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Revise(request.ExpectedRevision, request.Wording, request.Scope,
                    request.Priority, request.DueOn, request.ChangeReason, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
