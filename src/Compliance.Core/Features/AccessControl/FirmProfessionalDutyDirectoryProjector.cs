using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Projects each applied designation change into the catalog read model.</summary>
sealed partial class FirmProfessionalDutyDirectoryProjector(IFirmProfessionalDutyDirectoryProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForPattern(ProfessionalDutyContent.Realm,
            FirmProfessionalDutyDesignation.Area), FitzFirmProfessionalDutyDirectory.ProjectorName),
        IProjectorHandler<FirmProfessionalDutyDesignationRecorded>,
        IProjectorHandler<FirmProfessionalDutyDesignationRevoked>
{
    public ValueTask HandleAsync(FirmProfessionalDutyDesignationRecorded ev, IProjectorContext context,
        CancellationToken ct) => ProjectAsync(ev.DesignationContent, ct);

    public ValueTask HandleAsync(FirmProfessionalDutyDesignationRevoked ev, IProjectorContext context,
        CancellationToken ct) => ProjectAsync(ev.DesignationContent, ct);

    async ValueTask ProjectAsync(Proofs.ImmutableContentRef designation, CancellationToken ct) =>
        await projection.ApplyAsync(await ProfessionalDutyContent.ReadDesignationAsync(content, designation, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
}
