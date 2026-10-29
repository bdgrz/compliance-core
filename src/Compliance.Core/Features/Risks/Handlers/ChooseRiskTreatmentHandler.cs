using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Risks.Commands;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class ChooseRiskTreatmentHandler(RiskEvaluationOperations operations) : IRequestHandler<ChooseRiskTreatment>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ChooseRiskTreatment> context, CancellationToken ct) => operations.ChooseTreatmentAsync(context, ct);
}
