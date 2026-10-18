using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records a client member's response on the exact handoff that owns the feedback.</summary>
public sealed class RespondToAdvisorFeedbackHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RespondToAdvisorFeedback>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RespondToAdvisorFeedback> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Responding to advisor feedback requires a personal HTTP actor.")));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.FeedbackId == Uuid.Empty || string.IsNullOrWhiteSpace(request.Response))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An advisor feedback response requires its program, handoff, feedback, and response identities.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            var result = handoff.RespondToFeedback(request.FeedbackId, request.Response, actor,
                clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
