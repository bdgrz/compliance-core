using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class FitzCommitmentDraftDirectory(IKvClient client, CommitmentReader commitments)
    : FitzKvProjectionStore(client, "kv://bdgrz/commitment-draft-directory-v2/projection", ProjectorName),
        ICommitmentDraftDirectoryReader, ICommitmentDraftDirectoryProjection
{
    public const string ProjectorName = "CommitmentDraftDirectoryV2";
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
        if (value is not (CommitmentDraftCreated or CommitmentDraftRevised or CommitmentReviewed or CommitmentApproved or CommitmentRetired))
            return;
        var fact = CommitmentDirectoryFacts.Require(value);
        var header = fact.Header;
        if (_batchTenantId != header.TenantId)
            throw new InvalidOperationException("A commitment directory event cannot cross its batch tenant.");
        var current = await CommitmentDraftDirectorySchema.Drafts.GetAsync(Transaction, header.DraftId, ct).ConfigureAwait(false);
        if (value is CommitmentDraftCreated)
        {
            if (header.Revision != 1 || fact.NativeSequence != 1)
                throw new InvalidOperationException("A commitment identity must start with its exact native creation.");
            if (current is not null)
            {
                if (current.Header != header || current.NativeSequence != fact.NativeSequence)
                    throw new InvalidOperationException("A commitment identity cannot replace its original creation.");
                return;
            }
        }
        else
            CommitmentDirectoryFacts.RequireSuccessor(current, fact);
        if (value is CommitmentDraftRevised && header.Revision != current!.Header.Revision + 1 ||
            value is CommitmentReviewed or CommitmentApproved or CommitmentRetired && header.Revision != current!.Header.Revision)
            throw new InvalidOperationException("The commitment mutation has another exact content revision.");
        var row = new CommitmentDirectoryIdentity(header, fact.NativeSequence);
        if (current is null)
            await CommitmentDraftDirectorySchema.Drafts.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
        else
            await CommitmentDraftDirectorySchema.Drafts.ReplaceAsync(Transaction, current, row, ct).ConfigureAwait(false);
        if (value is CommitmentDraftCreated or CommitmentDraftRevised)
            await CommitmentDraftDirectorySchema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
        if (value is CommitmentReviewed reviewed)
        {
            var decision = row with { DecisionId = reviewed.DecisionId, DecidedAt = reviewed.DecidedAt };
            await CommitmentDraftDirectorySchema.Decisions.InsertAsync(Transaction, decision, ct).ConfigureAwait(false);
            if (reviewed.Version is { } version)
                await CommitmentDraftDirectorySchema.Versions.InsertAsync(Transaction, row with { Version = version }, ct).ConfigureAwait(false);
        }
        else if (value is CommitmentApproved approved)
        {
            var review = await CommitmentDraftDirectorySchema.Decisions.GetAsync(Transaction, approved.AcceptedReviewDecisionId, ct).ConfigureAwait(false);
            if (review is null || review.Header.DraftId != header.DraftId || review.Header.Revision != header.Revision ||
                review.Header.ProgramId != header.ProgramId || review.Header.TenantId != header.TenantId)
                throw new InvalidOperationException("A commitment approval must follow its exact accepted review.");
            await CommitmentDraftDirectorySchema.Decisions.InsertAsync(Transaction,
                row with { DecisionId = approved.DecisionId, DecidedAt = approved.DecidedAt }, ct).ConfigureAwait(false);
            await CommitmentDraftDirectorySchema.Versions.InsertAsync(Transaction, row with { Version = approved.Version }, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<CommitmentDraftView?> GetAsync(Uuid tenantId, Uuid draftId, CancellationToken ct = default)
    {
        CommitmentDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await CommitmentDraftDirectorySchema.Drafts.GetAsync(tx, draftId, ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, draftId, row, ct).ConfigureAwait(false)).Draft;
    }
    public async ValueTask<Page<CommitmentDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<CommitmentDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await CommitmentDraftDirectorySchema.Drafts.QueryAsync(tx,
                CommitmentDraftDirectorySchema.ByProgramKindIdentifier.Query().WithPrefix(programId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<CommitmentDraftView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.Header.ProgramId != programId)
                throw new ImmutableContentIntegrityException("A commitment directory row has another program scope.");
            values.Add((await SourceAsync(tenantId, row.Header.DraftId, row, ct).ConfigureAwait(false)).Draft
                ?? throw new ImmutableContentUnavailableException("The projected commitment draft is not visible at its native source."));
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid draftId, long revision,
        CancellationToken ct = default)
    {
        CommitmentDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await CommitmentDraftDirectorySchema.Revisions.GetAsync(tx,
                CommitmentDraftDirectorySchema.RevisionKey(draftId, revision), ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, draftId, row, ct).ConfigureAwait(false)).FindRevision(revision)
            ?? throw new ImmutableContentUnavailableException("The projected commitment revision is not visible at its native source.");
    }
    public async ValueTask<CommitmentVersionView?> GetVersionAsync(Uuid tenantId, Uuid draftId, long version,
        CancellationToken ct = default)
    {
        CommitmentDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await CommitmentDraftDirectorySchema.Versions.GetAsync(tx,
                CommitmentDraftDirectorySchema.RevisionKey(draftId, version), ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, draftId, row, ct).ConfigureAwait(false)).FindVersion(version)
            ?? throw new ImmutableContentUnavailableException("The projected commitment version is not visible at its native source.");
    }
    public async ValueTask<CommitmentDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid decisionId, CancellationToken ct = default)
    {
        CommitmentDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await CommitmentDraftDirectorySchema.Decisions.GetAsync(tx, decisionId, ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, row.Header.DraftId, row, ct).ConfigureAwait(false)).FindDecision(decisionId)
            ?? throw new ImmutableContentUnavailableException("The projected commitment decision is not visible at its native source.");
    }
    public async ValueTask<Page<CommitmentVersionView>> ListVersionsAsync(Uuid tenantId, Uuid draftId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<CommitmentDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await CommitmentDraftDirectorySchema.Versions.QueryAsync(tx,
                CommitmentDraftDirectorySchema.ByDraftVersion.Query().WithPrefix(draftId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<CommitmentVersionView>(page.Items.Count);
        foreach (var row in page.Items)
            values.Add((await SourceAsync(tenantId, draftId, row, ct).ConfigureAwait(false)).FindVersion(row.Version!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected commitment version is not visible at its native source."));
        return new(values, page.NextCursor);
    }
    public async ValueTask<Page<CommitmentDecisionView>> ListDecisionsAsync(Uuid tenantId, Uuid draftId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<CommitmentDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await CommitmentDraftDirectorySchema.Decisions.QueryAsync(tx,
                CommitmentDraftDirectorySchema.ByDraftDecision.Query().WithPrefix(draftId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<CommitmentDecisionView>(page.Items.Count);
        foreach (var row in page.Items)
            values.Add((await SourceAsync(tenantId, draftId, row, ct).ConfigureAwait(false)).FindDecision(row.DecisionId!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected commitment decision is not visible at its native source."));
        return new(values, page.NextCursor);
    }
    async ValueTask<CommitmentReadSnapshot> SourceAsync(Uuid tenantId, Uuid draftId, CommitmentDirectoryIdentity row, CancellationToken ct)
    {
        if (row.Header.TenantId != tenantId || row.Header.DraftId != draftId)
            throw new ImmutableContentIntegrityException("The commitment identity has another exact source scope.");
        var source = await commitments.ReadAsync(tenantId, row.Header.ProgramId, draftId, ct).ConfigureAwait(false);
        if (!source.IsCreated || source.Revision < row.Header.Revision || source.Kind != row.Header.Kind || source.Identifier != row.Header.Identifier ||
            source.ProgramId != row.Header.ProgramId || source.ServiceId != row.Header.ServiceId || source.ProviderId != row.Header.ProviderId)
            throw new ImmutableContentIntegrityException("The commitment identity differs from its native source.");
        return source;
    }
}
