using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class RecordAuditPackageBuildResultHandler(IAggregateExecutor executor,
    IDomainEventReader events) : IRequestHandler<RecordAuditPackageBuildResult>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordAuditPackageBuildResult> context, CancellationToken ct)
    {
        if (!RecordAuditPackageBuildResultAuthorizer.IsNamedWorker(context))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named audit package build worker may record build results."));

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PackageId == Uuid.Empty || request.BuildAttemptId == Uuid.Empty ||
            request.BuildRequestEventId == Uuid.Empty || request.Output is null ||
            request.CompletedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A build result requires the exact package attempt, request event, output, and completion time."));
        if (context.CausationId != request.BuildRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact audit package build request."));
        if (!await IsExactBuildRequestAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The request event does not match this exact audit package build attempt."));

        return await executor.ExecuteAsync(new AuditPackage(request.TenantId, request.ProgramId,
                request.PackageId), package =>
            {
                var current = package.Current;
                if (current is null || current.TenantId != request.TenantId ||
                    current.ProgramId != request.ProgramId || current.AuditPackageId != request.PackageId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The audit package was not found in this program.")));
                if (current.Versions.Count == 0 || current.Versions[^1].BuildAttempts.Count == 0 ||
                    current.Versions[^1].BuildAttempts[^1].BuildAttemptId != request.BuildAttemptId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The build result belongs to an earlier package version or attempt.")));

                return CommandFailureRequestAdapter.ToOutcome(package.RecordBuildResult(
                    package.Revision, request.BuildAttemptId, request.Output, null, "build-worker",
                    request.CompletedAt));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactBuildRequestAsync(RecordAuditPackageBuildResult request,
        CancellationToken ct)
    {
        var stream = new AuditPackage(request.TenantId, request.ProgramId, request.PackageId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.BuildRequestEventId)
                continue;
            return record.Stream == stream &&
                record.Event is AuditPackageBuildRequested build &&
                build.TenantId == request.TenantId && build.SubjectId == request.PackageId &&
                build.State is { } state && state.TenantId == request.TenantId &&
                state.ProgramId == request.ProgramId && state.AuditPackageId == request.PackageId &&
                state.Versions is { Count: > 0 } &&
                state.Versions[^1].BuildAttempts is { Count: > 0 } attempts &&
                attempts[^1].BuildAttemptId == request.BuildAttemptId &&
                attempts[^1].Status == "requested";
        }
        return false;
    }
}
