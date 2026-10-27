using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class FitzWorkforceObservationResolutionDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/workforce-observation-resolutions-v1/projection",
            "WorkforceObservationResolutionsV1"),
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
        var existing = await WorkforceObservationResolutionSchema.Resolutions.GetAsync(Transaction,
            resolved.ObservationId, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException("A workforce observation can be closed only once.");
        await WorkforceObservationResolutionSchema.Resolutions.InsertAsync(Transaction,
            new WorkforceObservationResolutionView(resolved.TenantId, resolved.ObservationId,
                resolved.Resolution, resolved.Note, resolved.Actor, resolved.ResolvedAt), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>>
        GetManyAsync(Uuid tenantId, IReadOnlyCollection<Uuid> observationIds,
            CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(observationIds);
        var found = new Dictionary<Uuid, WorkforceObservationResolutionView>();
        if (observationIds.Count == 0)
            return found;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        foreach (var observationId in observationIds.Distinct())
        {
            var view = await WorkforceObservationResolutionSchema.Resolutions.GetAsync(tx,
                observationId, ct).ConfigureAwait(false);
            if (view is not null && view.TenantId == tenantId)
                found[observationId] = view;
        }
        return found;
    }
}
