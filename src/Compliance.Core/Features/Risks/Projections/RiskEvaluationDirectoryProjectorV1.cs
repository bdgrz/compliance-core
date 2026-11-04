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
    public async ValueTask<Result> HandleAsync(IProjectorContext<RiskAssessmentRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RiskTreatmentChosen> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RiskAccepted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
