using Cntryl.Portia;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class AcceptRiskHandler(RiskEvaluationOperations operations, IAccessGrantPermissionAuthorizer permissions,
    RiskGovernanceReader governance, ISeparationOfDutiesWaiverProofResolver waivers) : IRequestHandler<AcceptRisk, RiskAcceptanceView>
{
    public ValueTask<Result<RiskAcceptanceView>> HandleAsync(IRequestContext<AcceptRisk> context, CancellationToken ct) =>
        operations.AcceptAsync(context, permissions, governance, waivers, ct);
}
