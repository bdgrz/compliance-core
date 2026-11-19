using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Queries;
namespace Bdgrz.Compliance.Features.Evidence.Models;

sealed class EvidenceRedactionAccess(EvidenceArtifactReadAccess artifacts, IPermissionAuthorizer permissions,
    IAccessGrantRolePermissionAuthorizer? roles = null)
    : IRequestAuthorizer<IEvidenceRedactionMutationRequest>, IRequestAuthorizer<GetEvidenceRedaction>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEvidenceRedactionMutationRequest> context, CancellationToken ct) =>
        RequireAsync(context, context.Request.TenantId, context.Request is ApproveEvidenceRedaction or ReviewEvidenceRedaction, ct);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<GetEvidenceRedaction> context, CancellationToken ct) =>
        artifacts.RequireMembershipAsync(context, context.Request.TenantId, ct);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId, bool approval, CancellationToken ct)
    {
        var member = await artifacts.RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!member.IsSuccess)
            return member;
        UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId);
        var memberId = RbacIds.Member(tenantId, userId);
        var permission = approval ? RbacPermissions.EvidenceRedactionApprove : RbacPermissions.EvidenceRedactionPrepare;
        var permitted = await permissions.IsAllowedAsync(tenantId, userId, memberId, permission, ct).ConfigureAwait(false);
        var roleAllowed = roles is not null &&
            (await roles.IsAllowedAsOrganizationRoleAsync(tenantId, userId, memberId,
                BuiltInRbac.ComplianceManagementRoleId(tenantId), permission, ct).ConfigureAwait(false) ||
             !approval && await roles.IsAllowedAsOrganizationRoleAsync(tenantId, userId, memberId,
                 BuiltInRbac.ComplianceParticipationRoleId(tenantId), permission, ct).ConfigureAwait(false));
        return permitted && roleAllowed ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Redaction preparation requires a current Contributor or Compliance Lead; review requires a Compliance Lead."));
    }
}
