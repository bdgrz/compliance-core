using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed partial class ComplianceIncidentDirectoryProjector(
    IComplianceIncidentDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), ProjectorName),
      IProjectorHandler<ComplianceIncidentRecorded>,
      IProjectorHandler<ComplianceIncidentRevised>,
      IProjectorHandler<ComplianceIncidentImpactAssessed>,
      IProjectorHandler<ComplianceIncidentSystemDescriptionDecisionRecorded>,
      IProjectorHandler<IncidentResolutionRecorded>
{
    public const string ProjectorName = "ComplianceIncidentDirectoryV1";

    public ValueTask HandleAsync(ComplianceIncidentRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ComplianceIncidentRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ComplianceIncidentImpactAssessed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ComplianceIncidentSystemDescriptionDecisionRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(IncidentResolutionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
