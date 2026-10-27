using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Applications.Readers;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationImportDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/application-import-directory/projection",
            "ApplicationImportDirectoryV1"), IApplicationImportDirectoryReader,
        IApplicationImportDirectoryProjection
{
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginImportBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginImportBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginImportBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An import projection batch requires its tenant realm.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyStagedAsync(ApplicationImportStaged staged,
        IReadOnlyList<ApplicationImportStagedRow> rows, CancellationToken ct = default)
    {
        if (staged.TenantId != _batchTenantId || rows.Count != staged.RowCount)
            throw new InvalidOperationException("An import observation projects in its tenant with its complete rows.");
        await ApplicationImportDirectorySchema.Batches.InsertAsync(Transaction,
            new ApplicationImportView(staged.TenantId, staged.BatchId, staged.SubmissionId,
                staged.SourceKey, staged.SourceNamespace, staged.Coverage,
                staged.ContentSha256, 1, "preview_ready", staged.ActorMemberId,
                staged.ActorDisplay, staged.SubmittedAt, staged.RowCount, staged.InvalidCount,
                0, 0, 0, 0, staged.SubmittedAt), ct).ConfigureAwait(false);
        foreach (var row in rows)
            await ApplicationImportDirectorySchema.Rows.InsertAsync(Transaction,
                    new ApplicationImportRowView(staged.TenantId, staged.BatchId, row.RowId,
                        row.RowNumber, row.SourceRecordId, row.Name, row.Purpose,
                        row.OwnerReference, row.ValidationFindings, "staged", null), ct)
                .ConfigureAwait(false);
    }

    public async ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default)
    {
        switch (ev)
        {
            case ApplicationImportFailed failed:
                await AdvanceAsync(failed.TenantId, failed.BatchId, failed.Revision, current => current with
                {
                    State = "failed",
                    PendingCount = 0,
                    AppliedCount = 0,
                    FailedCount = current.PendingCount,
                    LastProgressAt = failed.FailedAt,
                }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportCommitted committed:
                await AdvanceAsync(committed.TenantId, committed.BatchId, committed.Revision, current => current with
                {
                    State = "committed",
                    AppliedCount = current.PendingCount,
                    PendingCount = 0,
                    LastProgressAt = committed.CommittedAt,
                }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRetirementProposalSealed sealedProposal:
                await AdvanceAsync(sealedProposal.TenantId, sealedProposal.BatchId, sealedProposal.Revision,
                    current => current with { LastProgressAt = sealedProposal.Operation.AcceptedAt }, ct)
                    .ConfigureAwait(false);
                break;
            case ApplicationImportPlanSealed sealedPlan:
                await AdvanceAsync(sealedPlan.TenantId, sealedPlan.BatchId, sealedPlan.Revision, current => current with
                {
                    State = "accepting",
                    PendingCount = checked((int)sealedPlan.RowCount),
                    LastProgressAt = sealedPlan.Operation.AcceptedAt,
                }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportExecutionAuthorized authorized:
                await AdvanceAsync(_batchTenantId!.Value, Uuid.Parse(authorized.Owner.Resource!, null),
                    authorized.Revision, current => current with { LastProgressAt = authorized.Operation.AcceptedAt },
                    ct).ConfigureAwait(false);
                break;
            case ApplicationImportCancellationAccepted accepted:
                await AdvanceAsync(_batchTenantId!.Value, Uuid.Parse(accepted.Owner.Resource!, null),
                    accepted.Revision, current => current with
                    {
                        State = "canceling",
                        LastProgressAt = accepted.Operation.AcceptedAt,
                    }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportCanceled canceled:
                await AdvanceAsync(canceled.TenantId, canceled.BatchId, canceled.Revision, current => current with
                {
                    State = "canceled",
                    PendingCount = 0,
                    LastProgressAt = canceled.CanceledAt,
                }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRowCorrelated correlated:
                await AdvanceAsync(correlated.TenantId, correlated.BatchId, correlated.Revision,
                    current => current with { LastProgressAt = correlated.RecordedAt }, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask AdvanceAsync(Uuid tenantId, Uuid batchId, long revision,
        Func<ApplicationImportView, ApplicationImportView> change, CancellationToken ct)
    {
        var current = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction, batchId, ct)
            .ConfigureAwait(false);
        if (tenantId != _batchTenantId || current is null || current.TenantId != tenantId ||
            current.Revision + 1 != revision)
            throw new InvalidOperationException(
                "An import fact projects in its tenant, in order, after its staged observation.");
        await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, current,
            change(current) with { Revision = revision }, ct).ConfigureAwait(false);
    }

    public async ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationImportDirectorySchema.Batches.GetAsync(tx, batchId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId,
        Uuid batchId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationImportDirectorySchema.Rows.QueryAsync(tx,
                ApplicationImportDirectorySchema.ByBatchOrdinal.Query()
                    .WithPrefix(batchId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }
}
