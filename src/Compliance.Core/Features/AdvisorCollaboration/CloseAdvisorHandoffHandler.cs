using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public sealed class CloseAdvisorHandoffHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CloseAdvisorHandoff>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CloseAdvisorHandoff> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Closing an advisor handoff requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            var result = handoff.Close(request.Rationale, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
