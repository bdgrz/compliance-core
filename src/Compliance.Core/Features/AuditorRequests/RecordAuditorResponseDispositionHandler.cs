using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class RecordAuditorResponseDispositionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordAuditorResponseDisposition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAuditorResponseDisposition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording an auditor response disposition requires a personal HTTP actor.")));
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out var version))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The audit response version is invalid.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.RecordResponseDisposition(aggregate.Revision, request.ResponseId,
                    version, request.Disposition, request.ExternalReference, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
