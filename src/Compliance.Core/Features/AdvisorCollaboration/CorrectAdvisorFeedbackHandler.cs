using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Appends an attributed correction to feedback in its owning handoff stream.</summary>
public sealed class CorrectAdvisorFeedbackHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CorrectAdvisorFeedback>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CorrectAdvisorFeedback> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Correcting advisor feedback requires a personal HTTP actor.")));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty || request.FeedbackId == Uuid.Empty ||
            request.CorrectionId == Uuid.Empty || string.IsNullOrWhiteSpace(request.CorrectedBody) ||
            string.IsNullOrWhiteSpace(request.CorrectionReason))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A feedback correction requires tenant, program, handoff, feedback, correction, and content identities.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.CorrectFeedback(request.FeedbackId, request.CorrectionId,
                request.CorrectedBody, request.CorrectionReason, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
