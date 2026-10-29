using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
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
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Queries;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Programs.Authorizers;

sealed class ProgramManagementAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants,
    IAccessGrantPermissionAuthorizer scopedPermissions,
    IProgramResourceScopeResolver resourceScopes,
    ProfessionalAdvisoryReadAccess? professionalAdvisoryRead = null,
    IAccessGrantRolePermissionAuthorizer? rolePermissions = null)
    : IRequestAuthorizer<IProgramManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProgramManagementRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Program administration requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        if (context.Request is ListAdvisorRequests handoffs && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.GetListVisibilityAsync(tenantId,
                handoffs.ProgramId, userId, ct).ConfigureAwait(false) ==
            AdvisorHandoffListVisibility.SharedOnly)
            return Result.Success;
        if (context.Request is GetAdvisorSharedSource sharedSource &&
            professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanReadSelectedSourceAsync(tenantId,
                sharedSource.ProgramId, sharedSource.HandoffId, sharedSource.SourceFamily,
                sharedSource.RecordId, sharedSource.EntryId, userId, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is GetAdvisorRequest handoff && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanReadHandoffAsync(tenantId, handoff.ProgramId,
                handoff.AdvisorRequestId, userId, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is ListAdvisorFeedback feedback && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanReadHandoffAsync(tenantId, feedback.ProgramId,
                feedback.HandoffId, userId, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is CorrectAdvisorFeedback correction && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanCorrectFeedbackAsync(tenantId, correction.ProgramId,
                correction.HandoffId, correction.FeedbackId, userId, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is RecordAdvisorFeedback advisorFeedback)
            return professionalAdvisoryRead is not null &&
                   await professionalAdvisoryRead.CanRecordFeedbackAsync(tenantId,
                       advisorFeedback.ProgramId, advisorFeedback.HandoffId,
                       advisorFeedback.SelectedSource ?? new AdvisorRecordVersion(
                           advisorFeedback.RecordId, advisorFeedback.RecordVersionId), userId, ct)
                       .ConfigureAwait(false)
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The shared advisor handoff was not found."));
        if (context.Request is ListReadinessAnnotations annotations && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanReadAsync(tenantId, annotations.ProgramId, userId, ct)
                .ConfigureAwait(false))
            return Result.Success;

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (membership.Affiliation == "guest")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Guest members may access only records exposed by an active advisor handoff."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);

        if (context.Request is IProgramScopedRequest scoped)
        {
            if (!await resourceScopes.IsTenantProgramAsync(tenantId, scoped.ProgramId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program was not found."));
            if (RequiresComplianceLead(context.Request))
            {
                var complianceLead = rolePermissions is not null &&
                    await rolePermissions.IsAllowedAsRoleAsync(tenantId, userId, memberId,
                        scoped.ProgramId, BuiltInRbac.ComplianceManagementRoleId(tenantId),
                        scoped.RequiredPermission, ct).ConfigureAwait(false);
                return complianceLead
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                        "This advisor handoff operation requires a current Compliance Lead grant."));
            }
            var scopedAllowed = await scopedPermissions.IsAllowedAsync(tenantId, userId, memberId,
                    scoped.ProgramId, scoped.RequiredPermission, ct)
                .ConfigureAwait(false);
            return scopedAllowed
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not manage this program."));
        }

        if (context.Request is PreviewApplicationChange preview)
        {
            // The preview summarizes references across an application and may cross Programs.
            // A grant to one Program cannot authorize the whole tenant-owned response.
            if (!await resourceScopes.IsTenantApplicationAsync(tenantId, preview.ApplicationId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            var visibility = await scopedPermissions.GetProgramVisibilityAsync(tenantId, userId,
                    memberId, IProgramReadRequest.ReadPermission, ct)
                .ConfigureAwait(false);
            return visibility.OrganizationWide
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Application impact preview requires organization-wide program access."));
        }

        if (context.Request is DecideAccessReviewScope decision)
        {
            // M0-D05: the Compliance Lead approves scope. Until a dedicated role exists this is an
            // organization-wide program-management grant, not a grant to one Program.
            if (!await resourceScopes.IsTenantApplicationAsync(tenantId, decision.ApplicationId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            var visibility = await scopedPermissions.GetProgramVisibilityAsync(tenantId, userId,
                    memberId, IProgramScopedRequest.ManagementPermission, ct)
                .ConfigureAwait(false);
            return visibility.OrganizationWide
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Access-review scope approval requires organization-wide program management."));
        }

        if (context.Request is ListPrograms or ListClientServices)
        {
            var visibility = await scopedPermissions.GetProgramVisibilityAsync(tenantId, userId,
                    memberId, IProgramReadRequest.ReadPermission, ct)
                .ConfigureAwait(false);
            return visibility.HasAnyAccess
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not access any program in this organization."));
        }

        if (context.Request is IProgramResourceRequest resource)
        {
            var programId = await resourceScopes.ResolveProgramIdAsync(tenantId, resource, ct)
                .ConfigureAwait(false);
            if (programId is null)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program-owned resource was not found."));
            var allowed = await scopedPermissions.IsAllowedAsync(tenantId, userId, memberId,
                    programId.Value, resource.RequiredPermission, ct)
                .ConfigureAwait(false);
            return allowed
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not access this program-owned resource."));
        }

        // Program creation is organization-scoped; a grant to one existing program cannot
        // authorize creating another. Existing program records must carry or resolve that scope.
        if (context.Request is CreateProgram or SetCriteriaTextOverlay)
        {
            var organizationAccess = await scopedPermissions.GetProgramVisibilityAsync(tenantId,
                    userId, memberId, IProgramScopedRequest.ManagementPermission, ct)
                .ConfigureAwait(false);
            return organizationAccess.OrganizationWide
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "This operation requires organization-wide program management."));
        }

        return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The request does not identify an authorized program scope."));
    }

    static bool RequiresComplianceLead(IProgramManagementRequest request) => request is
        CreateAdvisorHandoff or ReviseAdvisorHandoffSelection or MarkAdvisorHandoffReadyForReview or
        PreviewAdvisorHandoff;
}
