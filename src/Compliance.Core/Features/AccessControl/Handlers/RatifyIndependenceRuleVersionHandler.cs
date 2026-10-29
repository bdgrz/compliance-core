using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class RatifyIndependenceRuleVersionHandler(IndependenceRuleRatificationOperations operations,
    ProfessionalDutyAuthorityReader duties)
    : IRequestHandler<RatifyIndependenceRuleVersion, IndependenceRuleRatificationView>
{
    public async ValueTask<Result<IndependenceRuleRatificationView>> HandleAsync(IRequestContext<RatifyIndependenceRuleVersion> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Ratification requires its command identity and exact authored catalog revision."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("IndependenceRuleRatificationAuthorizer must reject this actor.");
        // Current professional authority is required even for an original historical response.
        var currentDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.RuleRatifier, null, ct).ConfigureAwait(false);
        if (currentDuty is null)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor no longer has a current rule-ratifier designation."));
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await operations.RatifyAsync(context, currentDuty, actor, ct).ConfigureAwait(false);
    }
}
