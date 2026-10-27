using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

public sealed class RecordClientExportBuildResultHandler(IAggregateExecutor executor,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<RecordClientExportBuildResult>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordClientExportBuildResult> context, CancellationToken ct)
    {
        if (!RecordClientExportBuildResultAuthorizer.IsNamedWorker(context))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named client export build worker may record build results."));

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ExportId == Uuid.Empty ||
            request.ExpectedRevision <= 0 || request.BuildRequestEventId == Uuid.Empty ||
            request.Result is null || request.Result.AttemptId == Uuid.Empty ||
            request.CompletedAt == default || request.CompletedAt > clock.GetUtcNow())
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A build result requires its exact export attempt, request event, and completion time."));
        if (context.CausationId != request.BuildRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact client export build request."));
        if (!await IsExactBuildRequestAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request event does not match this exact client export build attempt."));

        var actor = ActorReference.ForSystemProcess(
            RecordClientExportBuildResultAuthorizer.ProcessId, "Client export build worker");
        return await executor.ExecuteAsync(new ClientExport(request.TenantId, request.ExportId), export =>
        {
            var current = export.Current;
            if (current is null || current.TenantId != request.TenantId ||
                current.ClientExportId != request.ExportId)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The client export was not found in this tenant.")));
            if (current.BuildAttempts.Any(attempt =>
                    attempt.AttemptId == request.Result.AttemptId))
                return CommandFailureRequestAdapter.ToOutcome(export.RecordBuildResult(
                    request.ExpectedRevision, request.Result, actor, request.CompletedAt));
            if (export.Revision != request.ExpectedRevision ||
                current.BuildAttempts.Count + 1 != request.Result.AttemptNumber ||
                current.Status is not ("requested" or "build_failed"))
                return AggregateOutcome.Discard(Result.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The build result belongs to an earlier export revision or attempt.")));

            return CommandFailureRequestAdapter.ToOutcome(export.RecordBuildResult(
                request.ExpectedRevision, request.Result, actor, request.CompletedAt));
        }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactBuildRequestAsync(RecordClientExportBuildResult request,
        CancellationToken ct)
    {
        var stream = new ClientExport(request.TenantId, request.ExportId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.BuildRequestEventId)
                continue;
            if (record.Stream != stream)
                return false;

            var state = record.Event switch
            {
                ClientExportRequested requested => requested.State,
                ClientExportBuildFailed failed => failed.State,
                _ => null,
            };
            if (state is null || state.TenantId != request.TenantId ||
                state.ClientExportId != request.ExportId ||
                state.Revision != request.ExpectedRevision ||
                request.Result.AttemptNumber != state.BuildAttempts.Count + 1 ||
                request.Result.AttemptId != Uuid.CreateVersion5(request.BuildRequestEventId,
                    $"client-export-build-attempt-v1:{request.Result.AttemptNumber}") ||
                request.Result.SourceSnapshotId != state.Content.SourceSnapshotId ||
                !StringComparer.OrdinalIgnoreCase.Equals(request.Result.SourceSnapshotSha256,
                    state.Content.SourceSnapshotSha256) || request.CompletedAt < state.UpdatedAt)
                return false;

            return record.Event switch
            {
                ClientExportRequested => state.Status == "requested" &&
                    request.Result.AttemptNumber == 1 && state.BuildAttempts.Count == 0,
                ClientExportBuildFailed => state.Status == "build_failed" &&
                    state.BuildAttempts.Count > 0 && state.BuildAttempts[^1].Outcome == "failed",
                _ => false,
            };
        }
        return false;
    }
}
