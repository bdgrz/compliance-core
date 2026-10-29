using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Providers.Readers;

sealed class FitzProviderDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/provider-register-v1/projection", ProjectorName), IProviderReader, IProviderProjection
{
    public const string ProjectorName = "ProviderRegisterV1";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginProviderBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginProviderBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginProviderBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != Provider.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("A provider projection batch requires its tenant's provider area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), Provider.Area)), ct);
    public async ValueTask ApplyAsync(DomainEvent domainEvent, ProviderContent content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var view = domainEvent switch
        {
            ProviderRecorded ev => ProviderSource.View(ev.TenantId, ev.ProviderId, 1, content, ev.Actor, ev.RecordedAt),
            ProviderRevised ev => ProviderSource.View(ev.TenantId, ev.ProviderId, ev.Revision, content, ev.Actor,
                ev.RecordedAt),
            _ => throw new InvalidOperationException("The provider event is not supported."),
        };
        if (view.TenantId == Uuid.Empty || view.ProviderId == Uuid.Empty || view.TenantId != _batchTenantId ||
            domainEvent is ProviderRevised { Revision: < 2 })
            throw new InvalidOperationException("A provider event must belong to its tenant projection batch.");
        var retained = await ProviderDirectorySchema.Revisions.GetAsync(Transaction,
            ProviderDirectorySchema.RevisionKey(view.ProviderId, view.Revision), ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!Same(retained, view))
                throw new InvalidOperationException("A provider revision cannot replace retained content.");
            return;
        }
        var current = await ProviderDirectorySchema.Providers.GetAsync(Transaction, view.ProviderId, ct)
            .ConfigureAwait(false);
        if (view.Revision < 1 || (current is null ? domainEvent is not ProviderRecorded :
                current.TenantId != view.TenantId || current.ProviderId != view.ProviderId ||
                current.Revision + 1 != view.Revision))
            throw new InvalidOperationException("A provider revision requires its tenant's immediate predecessor.");
        if (current is null)
            await ProviderDirectorySchema.Providers.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
        else
            await ProviderDirectorySchema.Providers.ReplaceAsync(Transaction, current, view, ct).ConfigureAwait(false);
        await ProviderDirectorySchema.Revisions.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
    }

    public async ValueTask<ProviderView?> GetAsync(Uuid tenantId, Uuid providerId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await ProviderDirectorySchema.Providers.GetAsync(tx, providerId, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId && view.ProviderId == providerId ? view : null;
    }

    public async ValueTask<ProviderView?> GetRevisionAsync(Uuid tenantId, Uuid providerId, long revision,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await ProviderDirectorySchema.Revisions.GetAsync(tx,
            ProviderDirectorySchema.RevisionKey(providerId, revision), ct).ConfigureAwait(false);
        return view?.TenantId == tenantId && view.ProviderId == providerId && view.Revision == revision ? view : null;
    }

    public async ValueTask<Page<ProviderView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProviderDirectorySchema.Providers.QueryAsync(tx,
            ProviderDirectorySchema.ByName.Query().Take(limit).After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ProviderView>> ListRevisionsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProviderDirectorySchema.Revisions.QueryAsync(tx,
            ProviderDirectorySchema.ByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    static bool Same(ProviderView left, ProviderView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.ProviderView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.ProviderView));
}
