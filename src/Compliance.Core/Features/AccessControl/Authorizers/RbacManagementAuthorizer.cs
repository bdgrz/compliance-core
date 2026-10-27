using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>
///     Row-level authorization for RBAC-management commands: reactors bootstrapping a tenant's RBAC
///     run as the trusted system actor and always pass, and any other caller needs
///     <see cref="RbacPermissions.TenantRbacManage" /> in the target tenant.
/// </summary>
sealed class RbacManagementAuthorizer(IPermissionAuthorizer permissions, ITenantActivity tenants,
    ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<IRbacManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IRbacManagementRequest> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return context.Request is GrantAccess or RevokeAccessGrant
                ? Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Access grant changes require an authenticated member."))
                : Result.Success;

        if (context.Request is DefineRole or RenameRole or AssignRolePermission or RemoveRolePermission)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The role catalog and permissions are fixed by the system."));
        if (context.Request is AssignTeamRole teamRole &&
            !BuiltInRbac.IsBuiltInRole(teamRole.TenantId, teamRole.RoleId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only built-in roles may be assigned to teams."));

        return await AuthorizeTenantAsync(context.Actor, context.Request.TenantId, ct).ConfigureAwait(false);
    }

    internal async ValueTask<Result> AuthorizeTenantAsync(ClaimsPrincipal actor, Uuid tenantId, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "RBAC management requires a Bdgrz user identity."));

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantRbacManage, ct)
            .ConfigureAwait(false);
        return allowed
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor may not manage this tenant's RBAC."));
    }
}
