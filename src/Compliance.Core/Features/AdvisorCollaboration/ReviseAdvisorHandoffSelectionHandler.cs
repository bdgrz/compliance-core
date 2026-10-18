using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Replaces a handoff's selected versions under an optimistic revision check.</summary>
public sealed class ReviseAdvisorHandoffSelectionHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAdvisorRecordVersionResolver recordVersions, TimeProvider clock)
    : IRequestHandler<ReviseAdvisorHandoffSelection>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseAdvisorHandoffSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising an advisor handoff requires a personal HTTP actor."));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A handoff selection revision requires program and handoff identities."));

        var exactSources = await recordVersions.RequireCurrentAsync(request.TenantId,
            request.ProgramId, request.RecordVersions, ct).ConfigureAwait(false);
        if (!exactSources.IsSuccess)
            return exactSources;

        var current = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
            request.ProgramId, request.HandoffId), ct).ConfigureAwait(false);
        if (!current.IsCreated || current.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));
        if (current.ComparedToHandoffId is { } comparedToId)
        {
            var previous = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
                request.ProgramId, comparedToId), ct).ConfigureAwait(false);
            if (!previous.IsCreated || previous.ProgramId != request.ProgramId ||
                !previous.WasReadyForReview ||
                previous.Status is not ("closed" or "revoked"))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The handoff comparison baseline is no longer available."));
            if (request.RecordVersions.Any(selected =>
                    AdvisorHandoffSelectionComparison.Classify(selected, previous.Selection) ==
                    AdvisorHandoffSourceChange.Unchanged))
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A repeated handoff may include only new or changed source versions."));
        }

        return await executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
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
        }, context, ct).ConfigureAwait(false);
    }
}
