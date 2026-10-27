using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class RecordRiskAssessmentHandler(RiskEvaluationOperations operations, RiskGovernanceReader governance)
    : IRequestHandler<RecordRiskAssessment, RiskAssessmentView>
{
    public ValueTask<Result<RiskAssessmentView>> HandleAsync(IRequestContext<RecordRiskAssessment> context, CancellationToken ct) =>
        operations.RecordAssessmentAsync(context, governance, ct);
}
