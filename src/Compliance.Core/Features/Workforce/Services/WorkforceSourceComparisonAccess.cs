using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
namespace Bdgrz.Compliance.Features.Workforce.Services;

static class WorkforceSourceComparisonAccess
{
    public static async ValueTask<Result> CheckAsync(IPermissionAuthorizer permissions, Uuid tenantId,
        ClaimsPrincipal actor, bool requiresRestrictedFields, CancellationToken ct)
    {
        if (!requiresRestrictedFields)
            return Result.Success;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(actor, out var subject)
            ? subject : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var manager = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        var reason = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        return manager.CanRead && reason.CanRead
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Comparing work relationship source facts requires both restricted field read grants."));
    }
}
