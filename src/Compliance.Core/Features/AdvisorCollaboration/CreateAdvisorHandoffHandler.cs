using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Creates one attributed draft handoff for a program's compliance lead.</summary>
public sealed class CreateAdvisorHandoffHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAdvisorRecordVersionResolver recordVersions, TimeProvider clock)
    : IRequestHandler<CreateAdvisorHandoff>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateAdvisorHandoff> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Creating an advisor handoff requires a personal HTTP actor."));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An advisor handoff requires program and handoff identities."));

        var exactSources = await recordVersions.RequireCurrentAsync(request.TenantId,
            request.ProgramId, request.RecordVersions, ct).ConfigureAwait(false);
        if (!exactSources.IsSuccess)
            return exactSources;

        if (request.ComparedToHandoffId is { } comparedToId)
        {
            if (comparedToId == Uuid.Empty || comparedToId == request.HandoffId)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A repeated handoff requires a distinct comparison handoff."));
            var previous = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
                request.ProgramId, comparedToId), ct).ConfigureAwait(false);
            if (!previous.IsCreated || previous.ProgramId != request.ProgramId)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The comparison handoff was not found in the requested program."));
            if (!previous.WasReadyForReview || previous.Status is not ("closed" or "revoked"))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A repeated handoff can compare only with a shared handoff that has ended."));
            if (request.RecordVersions.Any(selected =>
                    AdvisorHandoffSelectionComparison.Classify(selected, previous.Selection) ==
                    AdvisorHandoffSourceChange.Unchanged))
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A repeated handoff may include only new or changed source versions."));
        }

        return await executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (handoff.IsCreated && handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.Create(request.Purpose, request.RecordVersions,
                request.ExpiresOn, actor, clock.GetUtcNow(), request.ComparedToHandoffId);
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct).ConfigureAwait(false);
    }
}
