using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

sealed class FitzBoundaryDirectory(IKvClient client, BoundaryReader boundaries)
    : FitzKvProjectionStore(client, "kv://bdgrz/boundary-directory-v3/projection", ProjectorName),
        IBoundaryDirectoryReader, IBoundaryDirectoryProjection
{
    public const string ProjectorName = "BoundaryDirectoryV3";
    Uuid? _batchTenantId;
    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginBoundaryBatchAsync(context, ct);
    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginBoundaryBatchAsync(context, ct);
    async ValueTask<IProjectionBatch> BeginBoundaryBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (!Uuid.TryParse(context.Identity.Pattern.Realm, CultureInfo.InvariantCulture, out var tenant) || tenant == Uuid.Empty)
            throw new InvalidOperationException("A boundary directory batch requires its exact tenant scope.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenant;
        return batch;
    }
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString())), ct);

    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IBoundaryFact fact || value is not (BoundaryDraftCreated or BoundaryDraftRevised or BoundaryDraftDiscarded or
            BoundaryReviewed or BoundaryApproved or BoundarySuccessorProposed))
            return;
        BoundaryFactContent.ValidateScope(fact);
        var header = fact.Header;
        if (_batchTenantId != header.TenantId || fact.ChangeCount != 1 || fact.NativeSequence < 1 || header.Revision < 1 || !header.IsCreated)
            throw new InvalidOperationException("A boundary directory fact requires its exact native owner and batch tenant.");
        var current = await BoundaryDirectorySchema.Directory.GetAsync(Transaction, header.BoundaryId, ct).ConfigureAwait(false);
        if (current is not null && current.Header == header && current.NativeSequence == fact.NativeSequence)
            return;
        if (value is BoundaryDraftCreated)
        {
            if (current is not null || header.Revision != 1 || fact.NativeSequence != 1 || header.DraftRevision != 1)
                throw new InvalidOperationException("A boundary cannot project before its exact original creation.");
        }
        else if (current is null || current.Header.TenantId != header.TenantId || current.Header.ProgramId != header.ProgramId ||
            header.Revision != current.Header.Revision + 1 || fact.NativeSequence <= current.NativeSequence)
            throw new InvalidOperationException("A boundary mutation cannot project before its exact predecessor.");
        switch (value)
        {
            case BoundaryDraftRevised revised when current!.Header.DraftVersionId != revised.DraftVersionId ||
                current.Header.DraftRevision + 1 != revised.Revision:
                throw new InvalidOperationException("A boundary draft revision cannot project before its creation.");
            case BoundaryDraftDiscarded discarded when current!.Header.DraftVersionId != discarded.DraftVersionId ||
                current.Header.DraftRevision != discarded.Revision:
                throw new InvalidOperationException("A draft discard cannot project before its exact draft revision.");
            case BoundaryReviewed reviewed when current!.Header.DraftVersionId != reviewed.DraftVersionId ||
                current.Header.DraftRevision != reviewed.Revision:
                throw new InvalidOperationException("A boundary review cannot project before its exact draft revision.");
            case BoundaryApproved approved when current!.Header.DraftVersionId != approved.DraftVersionId ||
                current.Header.DraftRevision != approved.Revision || current.Header.AcceptedReviewDecisionId != approved.AcceptedReviewDecisionId:
                throw new InvalidOperationException("A boundary approval cannot project before its accepted review.");
            case BoundaryApproved scheduled when current!.Header.LatestApprovedEffectiveFrom is { } prior && scheduled.EffectiveFrom <= prior:
                throw new InvalidOperationException("A successor boundary version must become effective after its predecessor.");
            case BoundarySuccessorProposed proposed when current!.Header.LatestApprovedVersionId != proposed.PredecessorVersionId ||
                current.Header.DraftVersionId != Uuid.Empty:
                throw new InvalidOperationException("A successor cannot project before its approved predecessor.");
        }
        var row = new BoundaryDirectoryIdentity(header, fact.NativeSequence);
        if (!header.IsVisible)
        {
            if (current is not null)
                await BoundaryDirectorySchema.Directory.DeleteAsync(Transaction, current, ct).ConfigureAwait(false);
        }
        else if (current is null)
            await BoundaryDirectorySchema.Directory.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
        else
            await BoundaryDirectorySchema.Directory.ReplaceAsync(Transaction, current, row, ct).ConfigureAwait(false);
        if (value is BoundaryReviewed review)
            await BoundaryDirectorySchema.Decisions.InsertAsync(Transaction,
                row with { DecisionId = review.DecisionId, VersionId = review.DraftVersionId, DecidedAt = review.DecidedAt }, ct).ConfigureAwait(false);
        else if (value is BoundaryApproved approval)
        {
            await BoundaryDirectorySchema.Versions.InsertAsync(Transaction,
                row with { VersionId = approval.DraftVersionId, EffectiveFrom = approval.EffectiveFrom }, ct).ConfigureAwait(false);
            await BoundaryDirectorySchema.Decisions.InsertAsync(Transaction,
                row with { DecisionId = approval.ApprovalDecisionId, VersionId = approval.DraftVersionId, DecidedAt = approval.DecidedAt }, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId, CancellationToken ct = default)
    {
        BoundaryDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).ToView();
    }
    public async ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        Page<BoundaryDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await BoundaryDirectorySchema.Directory.QueryAsync(tx, BoundaryDirectorySchema.ByProgram.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<BoundaryView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.Header.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The boundary directory row has another program scope.");
            var source = await SourceAsync(tenantId, row.Header.BoundaryId, row, ct).ConfigureAwait(false);
            if (source.ToView() is { } view)
                values.Add(view);
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId, Uuid versionId,
        CancellationToken ct = default)
    {
        BoundaryDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await BoundaryDirectorySchema.Versions.GetAsync(tx, BoundaryDirectorySchema.ScopedKey(boundaryId, versionId), ct).ConfigureAwait(false);
        return row is null || row.Header.BoundaryId != boundaryId ? null
            : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).FindVersion(versionId)
                ?? throw new ImmutableContentUnavailableException("The projected boundary version is not visible at its native source.");
    }
    public async ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId, Uuid boundaryId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<BoundaryDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
        {
            if (await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct).ConfigureAwait(false) is null)
                return null;
            page = await BoundaryDirectorySchema.Versions.QueryAsync(tx, BoundaryDirectorySchema.VersionsByBoundary.Query()
                .WithPrefix(boundaryId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        }
        var values = new List<BoundaryVersionView>(page.Items.Count);
        BoundaryReadSnapshot? source = null;
        foreach (var row in page.Items)
        {
            source = await SourceAsync(tenantId, boundaryId, row, ct, source).ConfigureAwait(false);
            values.Add(source.FindVersion(row.VersionId!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected boundary version is not visible at its native source."));
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId, Uuid boundaryId, DateOnly effectiveOn,
        CancellationToken ct = default)
    {
        BoundaryDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct).ConfigureAwait(false);
        return row is null ? null : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).EffectiveVersion(effectiveOn);
    }
    public async ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId, Uuid decisionId,
        CancellationToken ct = default)
    {
        BoundaryDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await BoundaryDirectorySchema.Decisions.GetAsync(tx, BoundaryDirectorySchema.ScopedKey(boundaryId, decisionId), ct).ConfigureAwait(false);
        return row is null || row.Header.BoundaryId != boundaryId ? null
            : (await SourceAsync(tenantId, boundaryId, row, ct).ConfigureAwait(false)).FindDecision(decisionId)
                ?? throw new ImmutableContentUnavailableException("The projected boundary decision is not visible at its native source.");
    }
    public async ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId, Uuid boundaryId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<BoundaryDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
        {
            if (await BoundaryDirectorySchema.Directory.GetAsync(tx, boundaryId, ct).ConfigureAwait(false) is null)
                return null;
            page = await BoundaryDirectorySchema.Decisions.QueryAsync(tx, BoundaryDirectorySchema.DecisionsByBoundary.Query()
                .WithPrefix(boundaryId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        }
        var values = new List<BoundaryDecisionView>(page.Items.Count);
        BoundaryReadSnapshot? source = null;
        foreach (var row in page.Items)
        {
            source = await SourceAsync(tenantId, boundaryId, row, ct, source).ConfigureAwait(false);
            values.Add(source.FindDecision(row.DecisionId!.Value)
                ?? throw new ImmutableContentUnavailableException("The projected boundary decision is not visible at its native source."));
        }
        return new(values, page.NextCursor);
    }
    async ValueTask<BoundaryReadSnapshot> SourceAsync(Uuid tenantId, Uuid boundaryId, BoundaryDirectoryIdentity row,
        CancellationToken ct, BoundaryReadSnapshot? pinned = null)
    {
        if (row.Header.TenantId != tenantId || row.Header.BoundaryId != boundaryId)
            throw new ImmutableContentIntegrityException("The boundary identity has another exact source scope.");
        var source = pinned ?? await boundaries.ReadAsync(tenantId, row.Header.ProgramId, boundaryId, ct).ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != row.Header.ProgramId || source.Revision < row.Header.Revision)
            throw new ImmutableContentIntegrityException("The boundary identity differs from its authoritative source.");
        if (row.VersionId is null && row.DecisionId is null && source.IsVisible && source.Owner.Header != row.Header)
            throw new ImmutableContentUnavailableException("The boundary projection has not reached its native current authority.");
        return source;
    }
}
