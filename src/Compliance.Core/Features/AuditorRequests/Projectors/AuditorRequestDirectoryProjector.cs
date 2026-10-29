using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
namespace Bdgrz.Compliance.Features.AuditorRequests.Projectors;

public sealed partial class AuditorRequestDirectoryProjector(IAuditorRequestDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditorRequest.Area), FitzAuditorRequestDirectory.ProjectorName),
        IProjectorHandler<AuditorRequestRecorded>, IProjectorHandler<AuditorSampleSelectionAdmitted>
{
    public ValueTask HandleAsync(AuditorRequestRecorded fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
    public ValueTask HandleAsync(AuditorSampleSelectionAdmitted fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
}
