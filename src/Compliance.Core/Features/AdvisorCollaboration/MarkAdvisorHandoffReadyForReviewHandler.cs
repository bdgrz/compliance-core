using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Opens exact selected handoff versions to the assigned advisor or scoped guest.</summary>
public sealed class MarkAdvisorHandoffReadyForReviewHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<MarkAdvisorHandoffReadyForReview>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<MarkAdvisorHandoffReadyForReview> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Sharing an advisor handoff requires a personal HTTP actor.")));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Sharing an advisor handoff requires program and handoff identities.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.MarkReadyForReview(request.ExpectedRevision, actor,
                clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
