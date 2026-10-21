using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Advances one authorized, resumable source-offset step of finding history migration.</summary>
public sealed class MigrateFindingHistoryHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<MigrateFindingHistory>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<MigrateFindingHistory> context,
        CancellationToken ct)
    {
        if (!MigrateFindingHistoryAuthorizer.IsNamedWorker(context))
            return Forbidden();

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.MigrationId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation,
                "A finding migration needs tenant, program, and migration IDs.");

        var actor = ActorReference.ForSystemProcess(MigrateFindingHistoryAuthorizer.ProcessId,
            "Finding history migration");
        var migration = await reader.HydrateAsync(new FindingMigrationAggregate(
            request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
        if (!migration.IsStarted)
        {
            if (request.ExpectedSourceOffset is not null)
                return Failure(RequestErrorKind.Conflict,
                    "Start the finding migration before advancing a source offset.");
            return await StartAsync(context, actor, ct).ConfigureAwait(false);
        }

        if (migration.MigrationId != request.MigrationId || migration.SourceFence is not { } fence)
            return Failure(RequestErrorKind.Conflict,
                "A different finding migration owns this program source stream.");

        if (request.ExpectedSourceOffset is not { } expectedOffset)
            return await executor.ExecuteAsync(migration, current =>
                CommandFailureRequestAdapter.ToOutcome(current.Start(fence, actor,
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);

        if (expectedOffset > fence.MarkerResourceOffset)
            return Failure(RequestErrorKind.Validation,
                "The requested source offset is outside the committed finding migration fence.");

        if (expectedOffset == fence.MarkerResourceOffset)
            return await executor.ExecuteAsync(migration, current =>
                CommandFailureRequestAdapter.ToOutcome(current.Complete(fence.MarkerEventId,
                    fence.MarkerResourceOffset, current.ImportedFindingEventCount,
                    current.SourceDigest, actor, clock.GetUtcNow())), context, ct)
                .ConfigureAwait(false);

        if (expectedOffset < migration.NextSourceOffset)
        {
            if (expectedOffset != migration.NextSourceOffset - 1)
                return StaleOffset();
            var retriedRecord = await ReadRecordAtAsync(fence, expectedOffset, ct)
                .ConfigureAwait(false);
            if (retriedRecord is null || !migration.HasProgressed(retriedRecord))
                return StaleOffset();
            return Result.Success;
        }

        if (expectedOffset != migration.NextSourceOffset)
            return StaleOffset();

        var sourceRecord = await ReadRecordAtAsync(fence, expectedOffset, ct)
            .ConfigureAwait(false);
        if (sourceRecord is null)
            return Failure(RequestErrorKind.Conflict,
                "The next source event is missing from the fenced finding history.");

        if (!sourceRecord.Event.Metadata.IsAudit)
        {
            if (!FindingHistoryEventPayloads.TryIdentify(sourceRecord.Event,
                    request.TenantId, request.ProgramId, out var findingId, out _))
                return Failure(RequestErrorKind.Validation,
                    "The fenced source offset is not a supported finding fact or audit record.");

            var import = await executor.ExecuteAsync(new FindingAggregate(request.TenantId,
                    request.ProgramId, findingId), finding =>
                CommandFailureRequestAdapter.ToOutcome(finding.ImportLegacy(sourceRecord)),
                context, ct).ConfigureAwait(false);
            if (!import.IsSuccess)
                return import;
        }

        return await executor.ExecuteAsync(migration, current =>
            CommandFailureRequestAdapter.ToOutcome(current.Advance(sourceRecord,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    async ValueTask<Result> StartAsync(IRequestContext<MigrateFindingHistory> context,
        ActorReference actor, CancellationToken ct)
    {
        var request = context.Request;
        var fenceResult = await executor.ExecuteAsync(new RemediationMigrationFenceAggregate(
                request.TenantId, request.ProgramId), fence =>
                CommandFailureRequestAdapter.ToOutcome(fence.BeginPartitioning(
                    request.MigrationId, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
        if (!fenceResult.IsSuccess)
            return fenceResult;

        var fence = await ReadFenceAsync(request.TenantId, request.ProgramId,
            request.MigrationId, ct).ConfigureAwait(false);
        if (fence is null)
            return Failure(RequestErrorKind.Conflict,
                "The committed remediation fence was not found in its source stream.");

        return await executor.ExecuteAsync(new FindingMigrationAggregate(request.TenantId,
                request.ProgramId), migration => CommandFailureRequestAdapter.ToOutcome(
                migration.Start(fence, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<FindingMigrationFence?> ReadFenceAsync(Uuid tenantId, Uuid programId,
        Uuid migrationId, CancellationToken ct)
    {
        var stream = new RemediationMigrationFenceAggregate(tenantId, programId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
            if (record.Event is RemediationPartitioningStarted marker &&
                marker.MigrationId == migrationId)
                return FindingMigrationFence.FromMarker(record);
        return null;
    }

    async ValueTask<DomainEventRecord?> ReadRecordAtAsync(FindingMigrationFence fence,
        ulong sourceOffset, CancellationToken ct)
    {
        await foreach (var record in events.ReadAsync(fence.SourceStream, sourceOffset, ct)
                           .ConfigureAwait(false))
        {
            if (record.ResourceOffset == sourceOffset)
                return fence.IsBeforeMarker(record) ? record : null;
            if (record.ResourceOffset > sourceOffset)
                break;
        }
        return null;
    }

    static Result StaleOffset() => Failure(RequestErrorKind.Conflict,
        "The requested source offset does not match the durable finding migration cursor.");

    static Result Forbidden() => Failure(RequestErrorKind.Forbidden,
        "Only the named finding history migration process may advance this migration.");

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
