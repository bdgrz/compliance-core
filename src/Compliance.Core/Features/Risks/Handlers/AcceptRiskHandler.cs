using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class AcceptRiskHandler(RiskEvaluationOperations operations, IAccessGrantPermissionAuthorizer permissions,
    RiskGovernanceReader governance, ISeparationOfDutiesWaiverProofResolver waivers,
    IAccessGrantRolePermissionAuthorizer rolePermissions) : IRequestHandler<AcceptRisk, RiskAcceptanceView>
{
    public async ValueTask<Result<RiskAcceptanceView>> HandleAsync(IRequestContext<AcceptRisk> context, CancellationToken ct)
    {
        if (context.Request.ApproverAuthority == "compliance_lead")
        {
            var actor = RiskActor.From(context.Actor, context.Request.TenantId);
            if (!await rolePermissions.IsAllowedAsRoleAsync(context.Request.TenantId, actor.UserId, actor.MemberId,
                    context.Request.ProgramId, BuiltInRbac.ComplianceManagementRoleId(context.Request.TenantId),
                    "program.manage", ct).ConfigureAwait(false))
                return Result<RiskAcceptanceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Within-appetite acceptance requires a current Compliance Lead grant for this program."));
        }
        return await operations.AcceptAsync(context, permissions, governance, waivers, ct).ConfigureAwait(false);
    }
}
