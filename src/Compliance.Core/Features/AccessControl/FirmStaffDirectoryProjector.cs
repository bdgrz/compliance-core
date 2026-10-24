using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Projects each staff record's change facts into the directory read model.</summary>
sealed partial class FirmStaffDirectoryProjector(IFirmStaffDirectoryProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForPattern(FirmStaffContent.Realm, FirmStaffRecord.Area),
            FitzFirmStaffDirectory.ProjectorName),
        IProjectorHandler<FirmStaffChangeRecorded>
{
    public async ValueTask HandleAsync(FirmStaffChangeRecorded ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyAsync(await FirmStaffContent.ReadStaffAsync(content, ev.StaffContent, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);
}
