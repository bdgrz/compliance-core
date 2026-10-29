using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Requires a personal, current firm identity; client administration grants do not authorize acceptance.</summary>
sealed class ServiceEngagementAcceptanceAuthorizer(ProfessionalDutyAuthorityReader duties,
    IPlatformUserDirectoryReader platformUsers, ITenantActivity tenants)
    : IRequestAuthorizer<IServiceEngagementAcceptanceRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IServiceEngagementAcceptanceRequest> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires a personal HTTP request.");

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional acceptance requires a canonical Bdgrz user identity.");

        var tenantId = context.Request.TenantId;
        if (tenantId == Uuid.Empty || !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden, "The client tenant is not active.");

        if (!await platformUsers.ExistsAsync(userId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires a current platform user identity.");

        return await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner, tenantId, ct)
            .ConfigureAwait(false) is not null
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires the current partner designation for this client.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
