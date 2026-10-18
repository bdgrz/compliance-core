using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class ReviewAuditResponseHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewAuditResponse>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewAuditResponse> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var reviewerMemberId, out var reviewerDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Reviewing an audit response requires a personal HTTP actor.")));
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out var version))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The audit response version is invalid.")));
        return executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.ReviewResponse(aggregate.Revision, request.ResponseId, version,
                    request.Outcome, request.Rationale, reviewerMemberId, reviewerDisplay,
                    clock.GetUtcNow())), context, ct);
    }
}
