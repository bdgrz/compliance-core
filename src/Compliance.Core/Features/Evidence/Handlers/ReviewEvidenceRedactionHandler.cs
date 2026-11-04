using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ReviewEvidenceRedactionHandler(EvidenceRedactionOperations operations,
    IAccessGrantRolePermissionAuthorizer roles) : IRequestHandler<ReviewEvidenceRedaction, EvidenceRedactionView>
{
    public async ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<ReviewEvidenceRedaction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user) ||
            !await roles.IsAllowedAsOrganizationRoleAsync(request.TenantId, user, RbacIds.Member(request.TenantId, user),
                BuiltInRbac.ComplianceManagementRoleId(request.TenantId), RbacPermissions.EvidenceRedactionApprove, ct)
                .ConfigureAwait(false))
            return Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Redaction review requires a current organization Compliance Lead."));
        return await operations.ReviewAsync(context, ct).ConfigureAwait(false);
    }
}
