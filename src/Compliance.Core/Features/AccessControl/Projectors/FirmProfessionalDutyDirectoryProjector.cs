using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

/// <summary>Projects each applied designation change into the catalog read model.</summary>
sealed partial class FirmProfessionalDutyDirectoryProjector(IFirmProfessionalDutyDirectoryProjection projection,
    ProfessionalDutyReader dutyViews)
    : Projector(projection, EventStreamPattern.ForPattern(ProfessionalDutyContent.Realm,
            FirmProfessionalDutyDesignation.Area), FitzFirmProfessionalDutyDirectory.ProjectorName),
        IProjectorHandler<FirmProfessionalDutyDesignationRecorded>,
        IProjectorHandler<FirmProfessionalDutyDesignationRevoked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<FirmProfessionalDutyDesignationRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await ProjectAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<FirmProfessionalDutyDesignationRevoked> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await ProjectAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    async ValueTask ProjectAsync(DomainEvent designation, CancellationToken ct) =>
        await projection.ApplyAsync(await dutyViews.ReadAsync(designation, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
}
