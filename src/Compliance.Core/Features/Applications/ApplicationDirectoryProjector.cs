using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationDirectoryProjector(IApplicationDirectoryProjection projection, IAggregateReader reader,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(), "ApplicationDirectoryV2"),
      IProjectorHandler<ApplicationDeclared>, IProjectorHandler<ApplicationRevised>,
      IProjectorHandler<SystemInstanceDeclared>, IProjectorHandler<SystemInstanceRegistered>,
      IProjectorHandler<SystemInstanceRevised>, IProjectorHandler<ApplicationRetired>,
      IProjectorHandler<SystemInstanceRetired>,
      IProjectorHandler<ApplicationImportCommitted>
{
    public ValueTask HandleAsync(ApplicationDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public async ValueTask HandleAsync(ApplicationImportCommitted ev, IProjectorContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Realm != ev.TenantId.ToString())
            throw new InvalidOperationException("An import commit must belong to its tenant projection workload.");
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(ev.TenantId, ev.BatchId), ct)
            .ConfigureAwait(false);
        if (batch.Committed is not { Metadata: { } metadata } marker || marker != ev ||
            batch.CommittedStreamPosition < metadata.AggregateVersion)
            throw new InvalidOperationException("The import projection requires a durable authoritative batch commit.");
        ImportFrozenPlanIntent plan;
        IReadOnlyList<ApplicationImportPlannedRow> rows;
        IReadOnlyList<ApplicationImportEffectProof> proofs;
        try
        {
            plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, ev.TenantId, marker.Intent, ct)
                .ConfigureAwait(false);
            rows = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportPlannedRow>>(content,
                ev.TenantId, marker.FrozenRows, ct).ConfigureAwait(false);
            proofs = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportEffectProof>>(content,
                ev.TenantId, marker.EffectsManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new InvalidOperationException(
                $"The import projection requires verified immutable commit content: {exception.Message}");
        }

        foreach (var proof in proofs)
        {
            var target = await reader.HydrateAsync(new DeclaredApplication(ev.TenantId, proof.ApplicationId), ct)
                .ConfigureAwait(false);
            var effect = target.GetPendingImportEffect(ev.BatchId, proof.RowId);
            if (effect?.Metadata is not { } effectMetadata || effectMetadata.EventId != proof.EventId ||
                effectMetadata.AggregateVersion != proof.EventVersion ||
                target.CommittedStreamPosition < proof.EventVersion)
                throw new InvalidOperationException("The committed import target proof is missing or differs from its batch.");
        }

        await projection.ApplyCommittedImportAsync(ev, plan, rows, proofs, ct).ConfigureAwait(false);
    }
}
