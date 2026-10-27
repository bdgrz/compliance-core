using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationImportProjector(IApplicationImportDirectoryProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(ApplicationImportBatch.Area),
        "ApplicationImportDirectoryV1"), IProjectorHandler<ApplicationImportStaged>,
        IProjectorHandler<ApplicationImportCanceled>, IProjectorHandler<ApplicationImportRowCorrelated>,
        IProjectorHandler<ApplicationImportFailed>, IProjectorHandler<ApplicationImportPlanSealed>,
        IProjectorHandler<ApplicationImportExecutionAuthorized>, IProjectorHandler<ApplicationImportCancellationAccepted>,
        IProjectorHandler<ApplicationImportCommitted>, IProjectorHandler<ApplicationImportRetirementProposalSealed>
{
    public async ValueTask HandleAsync(ApplicationImportStaged ev, IProjectorContext context,
        CancellationToken ct)
    {
        IReadOnlyList<ApplicationImportStagedRow> rows;
        try
        {
            rows = (await ApplicationImportContent.ReadAsync<ApplicationImportRowsManifest>(content, ev.TenantId,
                ev.RowsManifest, ct).ConfigureAwait(false)).Rows;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new InvalidOperationException(
                $"The staged rows cannot be projected before their immutable content verifies: {exception.Message}");
        }

        await projection.ApplyStagedAsync(ev, rows, ct).ConfigureAwait(false);
    }

    public ValueTask HandleAsync(ApplicationImportCanceled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRowCorrelated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportFailed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportPlanSealed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportExecutionAuthorized ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportCancellationAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRetirementProposalSealed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportCommitted ev, IProjectorContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Realm != ev.TenantId.ToString())
            throw new InvalidOperationException("An import commit must belong to its tenant projection workload.");
        return projection.ApplyAsync(ev, ct);
    }
}
