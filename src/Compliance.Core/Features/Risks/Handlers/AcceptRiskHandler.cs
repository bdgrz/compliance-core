using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Independence;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

public sealed class AcceptRiskHandler(RiskEvaluationOperations operations, IAccessGrantPermissionAuthorizer permissions,
    RiskGovernanceReader governance, ISeparationOfDutiesWaiverProofResolver waivers) : IRequestHandler<AcceptRisk, RiskAcceptanceView>
{
    public ValueTask<Result<RiskAcceptanceView>> HandleAsync(IRequestContext<AcceptRisk> context, CancellationToken ct) =>
        operations.AcceptAsync(context, permissions, governance, waivers, ct);
}
