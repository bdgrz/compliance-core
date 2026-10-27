using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

/// <summary>Projects each staff record's change facts into the directory read model.</summary>
sealed partial class FirmStaffDirectoryProjector(IFirmStaffDirectoryProjection projection,
    FirmStaffViews views)
    : Projector(projection, EventStreamPattern.ForPattern(FirmStaffContent.Realm, FirmStaffRecord.Area),
            FitzFirmStaffDirectory.ProjectorName),
        IProjectorHandler<FirmStaffChangeRecorded>
{
    public async ValueTask HandleAsync(FirmStaffChangeRecorded ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyAsync(await views.ReadAsync(ev, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
}
