using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;

/// <summary>Requires the original personal client member's current Compliance Lead role at the target program.</summary>
public sealed class ApplyMethodologyTemplateV4Authorizer(ITenantMembershipDirectoryReader memberships,
    IAccessGrantRolePermissionAuthorizer permissions, TimeProvider clock) : IRequestAuthorizer<ApplyMethodologyTemplateV4>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyMethodologyTemplateV4> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Forbidden();
        var member = await memberships.GetAsync(request.TenantId.ToString(), user, ct).ConfigureAwait(false);
        if (member is null || member.TenantId != request.TenantId || member.UserId != user ||
            !member.IsActiveOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) || member.Affiliation is "firm_staff" or "guest")
            return Forbidden();
        return await permissions.IsAllowedAsRoleAsync(request.TenantId, user, RbacIds.Member(request.TenantId, user),
            request.ProgramId, BuiltInRbac.ComplianceManagementRoleId(request.TenantId), "program.manage", ct).ConfigureAwait(false)
            ? Result.Success : Forbidden();
    }

    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Applying firm methodology requires a current personal tenant Compliance Lead."));
}
