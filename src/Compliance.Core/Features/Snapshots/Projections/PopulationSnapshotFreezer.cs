using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

/// <summary>
///     Freezes caller-assembled population rows as an immutable snapshot keyed by the request ID,
///     so a retried request replays instead of freezing twice. Consumers own source capture and
///     authorization; this primitive owns identity, lineage, chunked storage, and the manifest
///     commit. A population larger than one storage chunk stores each chunk in its own stream
///     first; the snapshot becomes visible only when its manifest commits, and a retry resumes
///     an interrupted freeze because identical chunks replay.
/// </summary>
public sealed class PopulationSnapshotFreezer(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock)
{
    public async ValueTask<Result<SnapshotRegistration>> FreezeAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid tenantId, string kind,
        IReadOnlyList<PopulationRow> rows, Uuid? amendsSnapshotId, string? amendmentReason,
        ActorReference frozenBy, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if ((amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null &&
             (string.IsNullOrWhiteSpace(amendmentReason) || amendmentReason.Length > 4000)))
            return Failure(RequestErrorKind.Validation,
                "An amendment requires its predecessor and a reason of at most 4000 characters.");
        var digest = PopulationContentIdentity.Compute(kind, rows);
        if (!digest.IsSuccess)
            return Result<SnapshotRegistration>.Failure(digest.Error);
        var plan = PopulationSnapshotStorage.Plan(kind, rows);
        if (!plan.IsSuccess)
            return Result<SnapshotRegistration>.Failure(plan.Error);

        var snapshotId = context.RequestId;
        var rootSnapshotId = snapshotId;
        if (amendsSnapshotId is { } priorId)
        {
            if (priorId == snapshotId)
                return Failure(RequestErrorKind.Validation,
                    "An amendment cannot name itself as its predecessor.");
            var prior = await reader.HydrateAsync(new PopulationSnapshot(tenantId, priorId), ct)
                .ConfigureAwait(false);
            if (!prior.IsFrozen || prior.Kind != kind)
                return Failure(RequestErrorKind.NotFound, "The prior snapshot was not found.");
            // Creation leaves room for this amendment within the supported lineage depth.
            if (!await PopulationSnapshotLineage.IsBoundedAsync(reader, tenantId, prior,
                    SnapshotAmendmentLineage.MaximumAmendmentLinks - 1, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Conflict,
                    "The prior snapshot lineage is invalid or has reached the supported amendment limit.");
            rootSnapshotId = prior.RootSnapshotId;
        }

        var chunks = plan.Value;
        var frozenAt = clock.GetUtcNow();
        if (chunks.Count <= 1 && rows.Count <= PopulationSnapshot.MaximumInlineRows)
            return await executor.ExecuteAsync(new PopulationSnapshot(tenantId, snapshotId),
                snapshot => AggregateOutcome.CommitOnSuccess(snapshot.Freeze(rootSnapshotId,
                    amendsSnapshotId, kind, rows, digest.Value.Sha256, amendmentReason, frozenBy,
                    frozenAt)), context, ct).ConfigureAwait(false);

        foreach (var chunk in chunks)
        {
            IReadOnlyList<PopulationRow> chunkRows =
                [.. PopulationSnapshotStorage.Slice(rows, chunk.Start, chunk.Count)];
            var stored = await executor.ExecuteAsync(
                new PopulationSnapshotChunk(tenantId, snapshotId, chunk.Index),
                storage => AggregateOutcome.CommitOnSuccess(storage.Store(kind, chunkRows,
                    chunk.Sha256)), context, ct).ConfigureAwait(false);
            if (!stored.IsSuccess)
                return Result<SnapshotRegistration>.Failure(stored.Error);
        }
        return await executor.ExecuteAsync(new PopulationSnapshot(tenantId, snapshotId),
            snapshot => AggregateOutcome.CommitOnSuccess(snapshot.FreezeChunked(rootSnapshotId,
                amendsSnapshotId, kind, digest.Value, chunks, amendmentReason, frozenBy, frozenAt)),
            context, ct).ConfigureAwait(false);
    }

    static Result<SnapshotRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<SnapshotRegistration>.Failure(new RequestError(kind, message));
}
