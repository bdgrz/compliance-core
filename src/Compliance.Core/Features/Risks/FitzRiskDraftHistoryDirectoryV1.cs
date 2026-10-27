using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

sealed class FitzRiskDraftHistoryDirectoryV1(IKvClient client, IImmutableContentStore content, RiskDraftReader sources)
    : RiskDraftProjectionStore(client, content, sources, "kv://bdgrz/risk-draft-history-v2/projection", ProjectorName),
        IRiskDraftHistoryDirectoryReader, IRiskDraftHistoryDirectoryProjection
{
    public const string ProjectorName = "RiskDraftHistoryDirectoryV2";
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), RiskDraft.Area)), ct);
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        var row = await IdentityAsync(value, ct).ConfigureAwait(false);
        if (value is RiskDraftCreated)
        {
            if (row.Header.Revision != 1 || row.NativeSequence != 1)
                throw new InvalidOperationException("A risk draft history requires its exact creation.");
        }
        else
        {
            var previous = await RiskDraftHistoryDirectoryV1Schema.Revisions.GetAsync(Transaction,
                RiskDraftHistoryDirectoryV1Schema.RevisionKey(row.Header.RiskId, row.Header.Revision - 1), ct).ConfigureAwait(false);
            if (value is not RiskDraftRevised || row.Header.Revision < 2 || previous is null ||
                previous.Header.TenantId != row.Header.TenantId || previous.Header.ProgramId != row.Header.ProgramId ||
                previous.Header.Identifier != row.Header.Identifier || previous.NativeSequence + 1 != row.NativeSequence ||
                previous.Source.AggregateVersion >= row.Source.AggregateVersion)
                throw new InvalidOperationException("A risk draft history revision cannot project before its exact predecessor.");
        }
        await ValidateSourceAsync(value, ct).ConfigureAwait(false);
        await RiskDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId, long revision, CancellationToken ct = default)
    {
        RiskDraftDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await RiskDraftHistoryDirectoryV1Schema.Revisions.GetAsync(tx, RiskDraftHistoryDirectoryV1Schema.RevisionKey(riskId, revision), ct).ConfigureAwait(false);
        if (row is not null && (row.Header.RiskId != riskId || row.Header.Revision != revision))
            throw new ImmutableContentIntegrityException("The risk draft history row has another requested identity or revision.");
        return row is null ? null : await ReadAsync(tenantId, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
    {
        Page<RiskDraftDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await RiskDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(tx, RiskDraftHistoryDirectoryV1Schema.ByRiskRevision.Query()
                .WithPrefix(riskId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<RiskDraftRevisionView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.Header.RiskId != riskId)
                throw new ImmutableContentIntegrityException("The risk draft history page has another requested risk identity.");
            values.Add(await ReadAsync(tenantId, row, ct).ConfigureAwait(false));
        }
        return new(values, page.NextCursor);
    }
}
