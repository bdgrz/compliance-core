using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Chooses the recorded finding owner and bootstraps only a provably empty legacy stream.</summary>
static class FindingStreamRouting
{
    public static async ValueTask<Result> EnsureReadyAsync<TRequest>(IAggregateExecutor executor,
        IAggregateReader reader, IDomainEventReader events, IRequestContext<TRequest> context,
        Uuid tenantId, Uuid programId, DateTimeOffset now, CancellationToken ct) where TRequest : IRequestBase
    {
        var migration = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (migration.IsComplete)
            return Result.Success;
        var migrationId = Uuid.CreateVersion5(programId, "finding-history-v1-empty-bootstrap");
        if (migration.IsStarted && migration.MigrationId != migrationId)
            return MigrationRequired();
        var legacyStream = new RemediationMigrationFenceAggregate(tenantId, programId).Stream;
        FindingMigrationFence? fence = null;
        await foreach (var record in events.ReadAsync(legacyStream, 0, ct).ConfigureAwait(false))
        {
            if (record.ResourceOffset != 0 || record.Event is not RemediationPartitioningStarted marker ||
                marker.MigrationId != migrationId || marker.PreFenceProgramVersion != 0)
                return MigrationRequired();
            fence = FindingMigrationFence.FromMarker(record);
        }
        var actor = ActorReference.ForSystemProcess("migration:FindingHistoryV1", "Finding history migration");
        if (fence is null)
        {
            var fenced = await executor.ExecuteAsync(new RemediationMigrationFenceAggregate(tenantId, programId),
                aggregate => CommandFailureRequestAdapter.ToOutcome(aggregate.BeginPartitioning(migrationId,
                    actor, now, expectedPreFenceProgramVersion: 0)), context, ct).ConfigureAwait(false);
            if (!fenced.IsSuccess)
                return fenced;
            await foreach (var record in events.ReadAsync(legacyStream, 0, ct).ConfigureAwait(false))
            {
                if (record.ResourceOffset != 0 || record.Event is not RemediationPartitioningStarted marker ||
                    marker.MigrationId != migrationId || marker.PreFenceProgramVersion != 0)
                    return MigrationRequired();
                fence = FindingMigrationFence.FromMarker(record);
            }
        }
        if (fence is null || fence.MarkerResourceOffset != 0)
            return MigrationRequired();
        return await executor.ExecuteAsync(new FindingMigrationAggregate(tenantId, programId), current =>
        {
            var failure = current.Start(fence, actor, now);
            failure ??= current.Complete(fence.MarkerEventId, 0, 0, current.SourceDigest, actor, now);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
    }

    static Result MigrationRequired() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The legacy finding source requires its named history migration before business writes can resume."));
}
