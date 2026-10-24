using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteOrganizationMemberHandler(TenantInvitationAdmission admission)
    : IRequestHandler<InviteOrganizationMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<InviteOrganizationMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Member invitations require a Bdgrz user identity.")));
        return admission.AdmitAsync(context, request.InvitationId, request.TenantId, nameof(InviteOrganizationMember),
            AuthoredIntentDigest.Compute(request), TenantInvitationTerms.ForMember(request.EmailAddress,
                "client_personnel", false, request.BuiltInRole, userId), ct);
    }
}
