using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationRelationshipProjector(
    IApplicationRelationshipProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("applications"),
        "ApplicationRelationshipsV1"),
      IProjectorHandler<ApplicationRelationshipRecorded>,
      IProjectorHandler<ApplicationRelationshipRemoved>,
      IProjectorHandler<ApplicationRelationshipApproved>,
      IProjectorHandler<ApplicationRelationshipAdded>,
      IProjectorHandler<ApplicationRelationshipGovernedRemoved>
{
    public ValueTask HandleAsync(ApplicationRelationshipAdded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipRemoved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipApproved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipGovernedRemoved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
