using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class CassieWorkforceObservationResolutionDirectory(CassieProjectionDatabase database)
    : CassieWorkforceRepository(database, "WorkforceObservationResolutionsV1", "workforce-observation-resolutions"),
        IWorkforceObservationResolutionReader, IWorkforceObservationResolutionProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkforceObservationResolutionsV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "workforce-observation-resolutions")),
            ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not WorkforceObservationResolved resolved)
            return;
        var existing = await FindResolutionAsync(resolved.ObservationId, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException("A workforce observation can be closed only once.");
        await InsertResolutionAsync(new WorkforceObservationResolutionView(resolved.TenantId, resolved.ObservationId,
                resolved.Resolution, resolved.Note, resolved.Actor, resolved.ResolvedAt), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>>
        GetManyAsync(Uuid tenantId, IReadOnlyCollection<Uuid> observationIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(observationIds);
        var found = new Dictionary<Uuid, WorkforceObservationResolutionView>();
        if (observationIds.Count == 0)
            return found;
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        var scope = LiveProjectionKey(tenantId);
        foreach (var observationId in observationIds.Distinct())
        {
            await using var command = read.CreateCommand("SELECT row_json FROM workforce_observation_resolutions WHERE row_key = @key AND projection_key = @scope");
            Bind(command, ("key", RowKey(scope, observationId)), ("scope", scope));
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (value is string json)
            {
                var view = Deserialize(json, ComplianceCoreJsonContext.Default.WorkforceObservationResolutionView);
                if (view.TenantId == tenantId)
                    found[observationId] = view;
            }
        }
        return found;
    }

    ValueTask<WorkforceObservationResolutionView?> FindResolutionAsync(Uuid id, CancellationToken ct) =>
        FindCurrentAsync("workforce_observation_resolutions", id, ComplianceCoreJsonContext.Default.WorkforceObservationResolutionView, ct);

    ValueTask InsertResolutionAsync(WorkforceObservationResolutionView row, CancellationToken ct) =>
        InsertRowAsync("workforce_observation_resolutions", row.TenantId, row.ObservationId, row,
            ComplianceCoreJsonContext.Default.WorkforceObservationResolutionView, ct);
}
