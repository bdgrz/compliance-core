using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Restricts partner workflow requests to personal HTTP calls by the current designated client partner.</summary>
sealed class ProfessionalEngagementPartnerAuthorizer(ProfessionalDutyAuthorityReader duties,
    ITenantActivity tenants) : IRequestAuthorizer<IPersonalEngagementPartnerRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IPersonalEngagementPartnerRequest> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional partner review requires a personal HTTP request.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional partner review requires a canonical signed-in firm-staff identity.");
        var tenantId = context.Request.TenantId;
        if (tenantId == Uuid.Empty || !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden, "The client tenant is not active.");
        return await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner, tenantId, ct)
            .ConfigureAwait(false) is not null
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden,
                "Only the current, active partner designated for this client may review the engagement.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
