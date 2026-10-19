using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the responsible client's closure of one advisor feedback item.</summary>
public sealed class ResolveAdvisorFeedbackHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ResolveAdvisorFeedback>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ResolveAdvisorFeedback> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Resolving advisor feedback requires a personal HTTP actor.")));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.FeedbackId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Advisor feedback resolution requires program, handoff, and feedback identities.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.ResolveFeedback(request.FeedbackId, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
