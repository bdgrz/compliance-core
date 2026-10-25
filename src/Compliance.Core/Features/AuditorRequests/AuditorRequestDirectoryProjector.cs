using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public sealed partial class AuditorRequestDirectoryProjector(IAuditorRequestDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditorRequest.Area), FitzAuditorRequestDirectory.ProjectorName),
        IProjectorHandler<AuditorRequestRecorded>, IProjectorHandler<AuditorSampleSelectionAdmitted>
{
    public ValueTask HandleAsync(AuditorRequestRecorded fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
    public ValueTask HandleAsync(AuditorSampleSelectionAdmitted fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
}
