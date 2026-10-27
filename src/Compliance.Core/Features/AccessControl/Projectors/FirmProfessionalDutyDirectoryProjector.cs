using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

/// <summary>Projects each applied designation change into the catalog read model.</summary>
sealed partial class FirmProfessionalDutyDirectoryProjector(IFirmProfessionalDutyDirectoryProjection projection,
    ProfessionalDutyReader dutyViews)
    : Projector(projection, EventStreamPattern.ForPattern(ProfessionalDutyContent.Realm,
            FirmProfessionalDutyDesignation.Area), FitzFirmProfessionalDutyDirectory.ProjectorName),
        IProjectorHandler<FirmProfessionalDutyDesignationRecorded>,
        IProjectorHandler<FirmProfessionalDutyDesignationRevoked>
{
    public ValueTask HandleAsync(FirmProfessionalDutyDesignationRecorded ev, IProjectorContext context,
        CancellationToken ct) => ProjectAsync(ev, ct);

    public ValueTask HandleAsync(FirmProfessionalDutyDesignationRevoked ev, IProjectorContext context,
        CancellationToken ct) => ProjectAsync(ev, ct);

    async ValueTask ProjectAsync(DomainEvent designation, CancellationToken ct) =>
        await projection.ApplyAsync(await dutyViews.ReadAsync(designation, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
}
