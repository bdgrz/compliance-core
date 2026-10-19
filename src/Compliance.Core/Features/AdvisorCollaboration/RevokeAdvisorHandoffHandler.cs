using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public sealed class RevokeAdvisorHandoffHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevokeAdvisorHandoff>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokeAdvisorHandoff> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revoking an advisor handoff requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.Revoke(request.Rationale, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
