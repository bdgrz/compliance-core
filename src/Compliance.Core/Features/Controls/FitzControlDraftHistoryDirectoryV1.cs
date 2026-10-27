using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

sealed class FitzControlDraftHistoryDirectoryV1(IKvClient client, ControlReader controls)
    : FitzKvProjectionStore(client, "kv://bdgrz/control-draft-history-v2/projection", ProjectorName),
        IControlDraftHistoryDirectoryReader, IControlDraftHistoryDirectoryProjection
{
    public const string ProjectorName = "ControlDraftHistoryDirectoryV2";
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area)), ct);
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IControlFact fact || fact.ChangeCount == 0 ||
            value is not (ControlDraftCreated or ControlDraftRevised or ControlSuccessorProposed or ControlProposalWithdrawn))
            return;
        ControlFactContent.ValidateScope(fact);
        var header = fact.Header;
        var row = new ControlRevisionIdentity(header.TenantId, header.ProgramId, header.ControlId, header.Identifier,
            header.Revision, header.Content, fact.Change);
        if (header.Revision > 1 && await ControlDraftHistoryDirectoryV1Schema.Revisions.GetAsync(Transaction,
            ControlDraftHistoryDirectoryV1Schema.RevisionKey(header.ControlId, header.Revision - 1), ct).ConfigureAwait(false) is null)
            throw new InvalidOperationException("Control history must project its exact predecessor revision first.");
        var existing = await ControlDraftHistoryDirectoryV1Schema.Revisions.GetAsync(Transaction,
            ControlDraftHistoryDirectoryV1Schema.RevisionKey(header.ControlId, header.Revision), ct).ConfigureAwait(false);
        if (existing is not null && existing != row)
            throw new InvalidOperationException("A control revision identity cannot change its frozen source.");
        if (existing is null)
            await ControlDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId, long revision,
        CancellationToken ct = default)
    {
        ControlRevisionIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await ControlDraftHistoryDirectoryV1Schema.Revisions.GetAsync(tx,
                ControlDraftHistoryDirectoryV1Schema.RevisionKey(controlId, revision), ct).ConfigureAwait(false);
        return row is null ? null : await ComposeAsync(tenantId, controlId, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<Page<ControlDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid controlId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        Page<ControlRevisionIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await ControlDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(tx,
                ControlDraftHistoryDirectoryV1Schema.ByControlRevision.Query().WithPrefix(controlId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<ControlDraftRevisionView>(page.Items.Count);
        foreach (var row in page.Items)
            values.Add(await ComposeAsync(tenantId, controlId, row, ct).ConfigureAwait(false));
        return new Page<ControlDraftRevisionView>(values, page.NextCursor);
    }
    async ValueTask<ControlDraftRevisionView> ComposeAsync(Uuid tenantId, Uuid controlId, ControlRevisionIdentity row,
        CancellationToken ct)
    {
        if (row.TenantId != tenantId || row.ControlId != controlId)
            throw new ImmutableContentIntegrityException("The control history row has another exact scope.");
        return await controls.RevisionAsync(tenantId, row.ProgramId, controlId, row.Revision, ct).ConfigureAwait(false)
            ?? throw new ImmutableContentUnavailableException("The projected control revision is not visible at its native source.");
    }
}
