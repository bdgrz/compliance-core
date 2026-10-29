using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Applications.Authorizers;

sealed class ApplicationInventoryAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions,
    IAccessGrantScopePermissionAuthorizer scopedPermissions)
    : IRequestAuthorizer<IApplicationInventoryRequest>
{
    // A narrow V1 grant-backfill checkpoint replays TenantRegistered without replaying broader
    // tenant bootstrap side effects. Record-level policy remains a later authority decision.
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IApplicationInventoryRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Application inventory requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        if (await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.ApplicationInventoryManage, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is StageApplicationImport or GetApplicationImport or
                ListApplicationImportRows or PreviewApplicationImport or PreviewMissingApplicationImportRows or
                GetApplicationImportProgress or GetApplicationImportRejectedReport &&
            await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.ApplicationImportStage, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is IRestrictedApplicationResourceReadRequest &&
            await scopedPermissions.IsAllowedAtAnyApplicationInventoryScopeAsync(tenantId,
                userId, memberId, RbacPermissions.ApplicationRestrictedRead, ct)
                .ConfigureAwait(false))
            return Result.Success;
        return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The actor may not inspect or manage this inventory."));
    }
}
