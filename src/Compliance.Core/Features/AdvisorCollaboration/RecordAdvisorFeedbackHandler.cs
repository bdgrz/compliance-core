using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records attributed consultant feedback against an exact version in a shared handoff.</summary>
public sealed class RecordAdvisorFeedbackHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordAdvisorFeedback>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordAdvisorFeedback> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recording advisor feedback requires a personal HTTP actor.")));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty || request.FeedbackId == Uuid.Empty ||
            request.RecordId == Uuid.Empty ||
            request.SelectedSource is null && request.RecordVersionId == Uuid.Empty ||
            request.SelectedSource is { } selected && (!selected.IsSupported ||
                selected.RecordId != request.RecordId || selected.VersionId != request.RecordVersionId) ||
            string.IsNullOrWhiteSpace(request.Kind) || string.IsNullOrWhiteSpace(request.Body))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Advisor feedback requires tenant, program, handoff, feedback, record, version, kind, and body.")));

        var sourceVersion = request.SelectedSource ?? new AdvisorRecordVersion(request.RecordId,
            request.RecordVersionId);

        return executor.ExecuteAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
                request.HandoffId), handoff =>
        {
            if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
                return AggregateOutcome.CommitOnSuccess(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound,
                    "The advisor handoff was not found in the requested program.")));

            var result = handoff.RecordFeedback(request.FeedbackId, sourceVersion,
                request.Kind, request.Body, actor, clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                ? Result.Success : Result.Failure(result.Error));
        }, context, ct);
    }
}
