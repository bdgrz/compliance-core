using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

public sealed class RecordRiskAssessmentHandler(RiskEvaluationOperations operations, RiskGovernanceReader governance)
    : IRequestHandler<RecordRiskAssessment, RiskAssessmentView>
{
    public ValueTask<Result<RiskAssessmentView>> HandleAsync(IRequestContext<RecordRiskAssessment> context, CancellationToken ct) =>
        operations.RecordAssessmentAsync(context, governance, ct);
}
