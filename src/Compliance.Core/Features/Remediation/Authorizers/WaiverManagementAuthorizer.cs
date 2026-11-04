using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Remediation.Authorizers;

/// <summary>Requires an active client member and the approved role's product-waiver permission.</summary>
sealed class WaiverManagementAuthorizer(ITenantMembershipDirectoryReader memberships, ITenantActivity tenants,
    IPermissionAuthorizer permissions, TimeProvider clock) : IRequestAuthorizer<IWaiverManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IWaiverManagementRequest> context, CancellationToken ct)
    {
        if (context.Request is ReviewWaiver or RevokeWaiver &&
            WaiverActors.PersonalReviewError(context) is { } transportError)
            return transportError;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return new RequestError(RequestErrorKind.Unauthorized, "Product-waiver administration requires a signed-in client member.");
        var tenantId = context.Request.TenantId;
        var member = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (member is null || !member.IsActiveOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)))
            return new RequestError(RequestErrorKind.NotFound, "The tenant was not found.");
        if (member.Affiliation == "firm_staff")
            return new RequestError(RequestErrorKind.Forbidden, "Firm staff cannot administer client product waivers.");
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return new RequestError(RequestErrorKind.Forbidden, "The tenant is not active.");
        var permission = context.Request switch
        {
            RequestWaiver => WaiverPermissions.Request,
            ReviewWaiver or RevokeWaiver => WaiverPermissions.Review,
            _ => null,
        };
        return permission is not null && await permissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), permission, ct).ConfigureAwait(false)
            ? Result.Success
            : new RequestError(RequestErrorKind.Forbidden, "The member may not perform this product-waiver operation.");
    }
}
