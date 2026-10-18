using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationIncomingRelationshipProjector(
    IApplicationIncomingRelationshipProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("applications"), ProjectorName),
      IProjectorHandler<ApplicationRelationshipAdded>,
      IProjectorHandler<ApplicationRelationshipRemoved>,
      IProjectorHandler<ApplicationRelationshipRecorded>,
      IProjectorHandler<ApplicationRelationshipGovernedRemoved>,
      IProjectorHandler<ApplicationRelationshipApproved>
{
    public const string ProjectorName = "ApplicationIncomingRelationshipsV1";

    public ValueTask HandleAsync(ApplicationRelationshipAdded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipRemoved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipGovernedRemoved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipApproved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
