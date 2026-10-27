using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public sealed partial class RiskEvaluationDirectoryProjectorV1(
    IRiskEvaluationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risk-evaluations"),
            FitzRiskEvaluationDirectory.ProjectorName),
      IProjectorHandler<RiskAssessmentRecorded>, IProjectorHandler<RiskTreatmentChosen>,
      IProjectorHandler<RiskAccepted>
{
    public ValueTask HandleAsync(RiskAssessmentRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentChosen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
