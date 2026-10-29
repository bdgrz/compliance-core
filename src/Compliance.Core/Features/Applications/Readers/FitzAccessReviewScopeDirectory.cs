using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Readers;

sealed class FitzAccessReviewScopeDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/access-review-scope-directory-v2/projection",
            AccessReviewScopeStreams.ProjectorName),
        IAccessReviewScopeDirectoryReader, IAccessReviewScopeDirectoryProjection
{
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginScopeBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginScopeBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginScopeBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != AccessReviewScopeStreams.Area || context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenant) || tenant == Uuid.Empty)
            throw new InvalidOperationException("A scope projection batch requires its exact tenant source area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenant;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not AccessReviewScopeDecided ev)
            return;
        if (ev.TenantId != _batchTenantId)
            throw new InvalidOperationException("The scope source cannot cross its tenant projection batch.");
        var decision = await AccessReviewScopeReader.ReadDecisionAsync(content, ev, ct).ConfigureAwait(false);
        var existing = await AccessReviewScopeDirectorySchema.Scopes.GetAsync(Transaction,
            ev.SystemInstanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            if (ev.Sequence != 1)
                throw new InvalidOperationException(
                    "A scope decision cannot project before its predecessor.");
            await AccessReviewScopeDirectorySchema.Scopes.InsertAsync(Transaction,
                new AccessReviewScopeRecord(ev.TenantId, ev.ApplicationId, ev.SystemInstanceId,
                    [decision]), ct).ConfigureAwait(false);
            return;
        }
        if (existing.TenantId != ev.TenantId || existing.ApplicationId != ev.ApplicationId || existing.SystemInstanceId != ev.SystemInstanceId)
            throw new InvalidOperationException("A scope projection cannot replace another exact parent or instance.");
        if (ev.Sequence <= existing.Decisions.Count)
        {
            if (existing.Decisions[(int)ev.Sequence - 1] != decision)
                throw new InvalidOperationException("A projected scope decision cannot replace its original body.");
            return;
        }
        if (ev.Sequence != existing.Decisions.Count + 1)
            throw new InvalidOperationException(
                "A scope decision cannot project before its predecessor.");
        await AccessReviewScopeDirectorySchema.Scopes.ReplaceAsync(Transaction, existing,
            existing with { Decisions = [.. existing.Decisions, decision] }, ct)
            .ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(AccessReviewScopeStreams.ProjectorName,
            AccessReviewScopeStreams.TenantPattern(tenantId)), ct);

    public async ValueTask<AccessReviewScopeRecord?> GetAsync(Uuid tenantId,
        Uuid systemInstanceId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AccessReviewScopeDirectorySchema.Scopes.GetAsync(tx, systemInstanceId, ct)
            .ConfigureAwait(false);
    }
}
