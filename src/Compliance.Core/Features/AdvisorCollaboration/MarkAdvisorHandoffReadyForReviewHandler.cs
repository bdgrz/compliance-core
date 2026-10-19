using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Opens exact selected handoff versions to the assigned advisor or scoped guest.</summary>
public sealed class MarkAdvisorHandoffReadyForReviewHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAdvisorRecordVersionResolver recordVersions, TimeProvider clock)
    : IRequestHandler<MarkAdvisorHandoffReadyForReview>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<MarkAdvisorHandoffReadyForReview> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Sharing an advisor handoff requires a personal HTTP actor."));
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Sharing an advisor handoff requires program and handoff identities."));

        var selectedHandoff = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
            request.ProgramId, request.HandoffId), ct).ConfigureAwait(false);
        if (!selectedHandoff.IsCreated || selectedHandoff.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));
        var selection = selectedHandoff.Selection;
        var exactSources = await recordVersions.RequireCurrentAsync(request.TenantId,
            request.ProgramId, selection, ct).ConfigureAwait(false);
        if (!exactSources.IsSuccess)
            return exactSources;

        return await executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));
            if (!handoff.Selection.SequenceEqual(selection))
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The handoff selection changed while its sources were being checked.")));

            var result = handoff.MarkReadyForReview(request.ExpectedRevision, actor,
                clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct).ConfigureAwait(false);
    }
}
