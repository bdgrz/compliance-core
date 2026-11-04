using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class CassieWorkforceSourceDirectory(CassieProjectionDatabase database)
    : CassieWorkforceRepository(database, "WorkforceSourcesV1", "workforce-source-observations"),
        IWorkforceSourceDirectoryReader, IWorkforceSourceProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkforceSourcesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "workforce-source-observations")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkforceSourceObserved observed:
                await InsertSourceAsync(new WorkforceSourceView(observed.TenantId, observed.ObservationId, 1, observed.Source,
                        observed.TargetKind, observed.TargetId, observed.ObservedTargetRevision, observed.Facts,
                        observed.ObservedAt, observed.Actor, observed.RecordedAt), ct).ConfigureAwait(false);
                break;
            case WorkforceSourceReconciled reconciled:
                var current = await FindSourceAsync(reconciled.ObservationId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != reconciled.TenantId ||
                    current.Decision is not null || current.Revision + 1 != reconciled.Revision)
                    throw new InvalidOperationException("A source decision requires its unreconciled predecessor.");
                await ReplaceSourceAsync(current with { Revision = reconciled.Revision, Decision = reconciled.Decision }, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<WorkforceSourceView?> GetAsync(Uuid tenantId, Uuid observationId, CancellationToken ct = default)
    {
        var view = await ReadRowAsync("workforce_source_observations", tenantId, observationId,
            ComplianceCoreJsonContext.Default.WorkforceSourceView, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public ValueTask<Page<WorkforceSourceView>> ListAsync(Uuid tenantId, string? targetKind,
        Uuid? targetId, int limit, string? cursor, CancellationToken ct = default) =>
        targetKind is not null && targetId is { } id
            ? ReadPageAsync("workforce_source_observations", tenantId, ComplianceCoreJsonContext.Default.WorkforceSourceView,
                limit, cursor, $"target:{targetKind.Length}:{targetKind}:{id}", ct, "model_id",
                " AND target_kind = @kind AND target_id = @target", ("kind", targetKind), ("target", id.ToString()))
            : ReadPageAsync("workforce_source_observations", tenantId, ComplianceCoreJsonContext.Default.WorkforceSourceView,
                limit, cursor, "observed", ct);

    ValueTask<WorkforceSourceView?> FindSourceAsync(Uuid id, CancellationToken ct) =>
        FindCurrentAsync("workforce_source_observations", id, ComplianceCoreJsonContext.Default.WorkforceSourceView, ct);

    ValueTask InsertSourceAsync(WorkforceSourceView row, CancellationToken ct) =>
        InsertRowAsync("workforce_source_observations", row.TenantId, row.ObservationId, row,
            ComplianceCoreJsonContext.Default.WorkforceSourceView, ct, ("sort_key", row.ObservedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            ("target_kind", row.TargetKind), ("target_id", row.TargetId.ToString()));

    ValueTask ReplaceSourceAsync(WorkforceSourceView row, CancellationToken ct) =>
        ReplaceRowAsync("workforce_source_observations", row.TenantId, row.ObservationId, row,
            ComplianceCoreJsonContext.Default.WorkforceSourceView, ct, ("sort_key", row.ObservedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            ("target_kind", row.TargetKind), ("target_id", row.TargetId.ToString()));
}
