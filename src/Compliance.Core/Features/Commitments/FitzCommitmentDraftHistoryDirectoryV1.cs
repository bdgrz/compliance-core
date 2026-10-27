using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Projections;
using Bdgrz.Compliance.Features.Commitments.Readers;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class FitzCommitmentDraftHistoryDirectoryV1(IKvClient client, CommitmentReader commitments)
    : FitzKvProjectionStore(client, "kv://bdgrz/commitment-draft-history-v2/projection", ProjectorName),
        ICommitmentDraftHistoryDirectoryReader, ICommitmentDraftHistoryDirectoryProjection
{
    public const string ProjectorName = "CommitmentDraftHistoryDirectoryV2";
    Uuid? _batchTenantId;
    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginCommitmentBatchAsync(context, ct);
    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginCommitmentBatchAsync(context, ct);
    async ValueTask<IProjectionBatch> BeginCommitmentBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != CommitmentDraft.Area || context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, CultureInfo.InvariantCulture, out var tenant) || tenant == Uuid.Empty)
            throw new InvalidOperationException("A commitment directory batch requires its tenant's exact commitment area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenant;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts")), ct);
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not (CommitmentDraftCreated or CommitmentDraftRevised))
            return;
        var fact = CommitmentDirectoryFacts.Require(value);
        var header = fact.Header;
        if (_batchTenantId != header.TenantId)
            throw new InvalidOperationException("A commitment directory event cannot cross its batch tenant.");
        var row = new CommitmentDirectoryIdentity(header, fact.NativeSequence);
        if (header.Revision > 1)
        {
            var predecessor = await CommitmentDraftHistoryDirectoryV1Schema.Revisions.GetAsync(Transaction,
                CommitmentDraftHistoryDirectoryV1Schema.RevisionKey(header.DraftId, header.Revision - 1), ct).ConfigureAwait(false);
            CommitmentDirectoryFacts.RequireSuccessor(predecessor, fact);
        }
        var existing = await CommitmentDraftHistoryDirectoryV1Schema.Revisions.GetAsync(Transaction,
            CommitmentDraftHistoryDirectoryV1Schema.RevisionKey(header.DraftId, header.Revision), ct).ConfigureAwait(false);
        if (existing is not null && existing != row)
            throw new InvalidOperationException("A commitment revision cannot replace its original immutable source.");
        if (existing is null)
            await CommitmentDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid draftId, long revision,
        CancellationToken ct = default)
    {
        CommitmentDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await CommitmentDraftHistoryDirectoryV1Schema.Revisions.GetAsync(tx,
                CommitmentDraftHistoryDirectoryV1Schema.RevisionKey(draftId, revision), ct).ConfigureAwait(false);
        return row is null ? null : await ComposeAsync(tenantId, draftId, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<Page<CommitmentDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid draftId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<CommitmentDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await CommitmentDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(tx,
                CommitmentDraftHistoryDirectoryV1Schema.ByDraftRevision.Query().WithPrefix(draftId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<CommitmentDraftRevisionView>(page.Items.Count);
        foreach (var row in page.Items)
            values.Add(await ComposeAsync(tenantId, draftId, row, ct).ConfigureAwait(false));
        return new(values, page.NextCursor);
    }
    async ValueTask<CommitmentDraftRevisionView> ComposeAsync(Uuid tenantId, Uuid draftId,
        CommitmentDirectoryIdentity row, CancellationToken ct)
    {
        if (row.Header.TenantId != tenantId || row.Header.DraftId != draftId)
            throw new ImmutableContentIntegrityException("The commitment history row has another exact scope.");
        var source = await commitments.ReadAsync(tenantId, row.Header.ProgramId, draftId, ct).ConfigureAwait(false);
        return source.FindRevision(row.Header.Revision)
            ?? throw new ImmutableContentUnavailableException("The projected commitment revision is not visible at its native source.");
    }
}
