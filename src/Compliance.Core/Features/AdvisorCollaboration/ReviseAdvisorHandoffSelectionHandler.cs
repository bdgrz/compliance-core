using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Replaces a handoff's selected versions under an optimistic revision check.</summary>
public sealed class ReviseAdvisorHandoffSelectionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseAdvisorHandoffSelection>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAdvisorHandoffSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising an advisor handoff requires a personal HTTP actor.")));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A handoff selection revision requires program and handoff identities.")));

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.ReviseSelection(request.ExpectedRevision, request.RecordVersions,
                request.ChangeReason, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
