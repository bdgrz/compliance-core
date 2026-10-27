using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Readers;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

sealed class FitzRiskEvaluationDirectory(IKvClient client, RiskEvaluationReader sources)
    : FitzKvProjectionStore(client, "kv://bdgrz/risk-evaluation-directory-v2/projection", ProjectorName),
        IRiskEvaluationDirectoryReader, IRiskEvaluationDirectoryProjection
{
    public const string ProjectorName = "RiskEvaluationDirectoryV2";
    Uuid? _batchTenant;
    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) => BeginEvaluationBatchAsync(context, ct);
    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) => BeginEvaluationBatchAsync(context, ct);
    async ValueTask<IProjectionBatch> BeginEvaluationBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (!Uuid.TryParse(context.Identity.Pattern.Realm, CultureInfo.InvariantCulture, out var tenant) || tenant == Uuid.Empty)
            throw new InvalidOperationException("An evaluation projection batch requires its exact tenant scope.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenant = tenant;
        return batch;
    }
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), RiskEvaluation.Area)), ct);
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IRiskEvaluationFact fact)
            return;
        RiskEvaluationNativeFact.Require(value, true);
        if (_batchTenant != fact.TenantId)
            throw new ImmutableContentIntegrityException("An evaluation projection requires its bound tenant.");
        await sources.ValidateFactAsync(value, ct).ConfigureAwait(false);
        var prior = await RiskEvaluationDirectorySchema.Evaluations.GetAsync(Transaction, fact.RiskId, ct).ConfigureAwait(false);
        var next = new RiskEvaluationDirectoryIdentity(fact.TenantId, fact.ProgramId, fact.RiskId, fact.Revision, fact.NativeSequence);
        if (prior == next)
            return;
        if (prior is null ? fact.Revision != 1 || fact.NativeSequence != 1 : prior.TenantId != fact.TenantId || prior.ProgramId != fact.ProgramId ||
            prior.Revision + 1 != fact.Revision || prior.NativeSequence + 1 != fact.NativeSequence)
            throw new InvalidOperationException("An evaluation projection cannot precede its exact native predecessor.");
        if (prior is null)
            await RiskEvaluationDirectorySchema.Evaluations.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
            await RiskEvaluationDirectorySchema.Evaluations.ReplaceAsync(Transaction, prior, next, ct).ConfigureAwait(false);
        await RiskEvaluationDirectorySchema.History.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId, CancellationToken ct = default)
    {
        RiskEvaluationDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await RiskEvaluationDirectorySchema.Evaluations.GetAsync(tx, riskId, ct).ConfigureAwait(false);
        if (row is null)
            return null;
        var source = await ReadAsync(tenantId, riskId, row, ct).ConfigureAwait(false);
        if (source.Revision != row.Revision)
            throw new ImmutableContentUnavailableException("The evaluation projection has not reached its current native authority.");
        return source.ToView();
    }
    public async ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId, Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
    {
        Page<RiskEvaluationDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await RiskEvaluationDirectorySchema.History.QueryAsync(tx, RiskEvaluationDirectorySchema.ByRiskRevision.Query()
                .WithPrefix(riskId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<RiskEvaluationHistoryEntryView>(page.Items.Count);
        RiskEvaluationReadSnapshot? source = null;
        foreach (var row in page.Items)
        {
            source ??= await ReadAsync(tenantId, riskId, row, ct).ConfigureAwait(false);
            if (row.TenantId != tenantId || row.ProgramId != source.ProgramId || row.RiskId != riskId || row.Revision > source.Revision)
                throw new ImmutableContentIntegrityException("The evaluation history identity differs from its exact native owner.");
            var fact = source.Facts.SingleOrDefault(value => value is IRiskEvaluationFact native && native.Revision == row.Revision && native.NativeSequence == row.NativeSequence)
                ?? throw new ImmutableContentUnavailableException("The exact evaluation history source is not visible yet.");
            values.Add(await sources.HistoryEntryAsync(fact, ct).ConfigureAwait(false));
        }
        return new(values, page.NextCursor);
    }
    async ValueTask<RiskEvaluationReadSnapshot> ReadAsync(Uuid tenantId, Uuid riskId, RiskEvaluationDirectoryIdentity row, CancellationToken ct)
    {
        if (row.TenantId != tenantId || row.RiskId != riskId)
            throw new ImmutableContentIntegrityException("The evaluation identity has another exact tenant or owner.");
        var source = await sources.ReadAsync(tenantId, row.ProgramId, riskId, ct).ConfigureAwait(false);
        if (source.ProgramId != row.ProgramId || source.Revision < row.Revision || source.Owner.NativeSequence < row.NativeSequence)
            throw new ImmutableContentUnavailableException("The projected evaluation source is not visible yet.");
        return source;
    }
}
