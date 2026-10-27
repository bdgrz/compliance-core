using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteTenantMemberHandler(TenantInvitationAdmission admission, IAggregateReader reader)
    : IRequestHandler<InviteTenantMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<InviteTenantMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var tenant = await reader.HydrateAsync(new Tenant(request.TenantId), ct).ConfigureAwait(false);
        if (!tenant.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant does not exist."));

        var invitedBy = RequestActor.IsSystem(context.Actor)
            ? tenant.OperatorUserId
            : UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
                ? userId
                : Uuid.Empty;
        return await admission.AdmitAsync(context, request.InvitationId, request.TenantId, nameof(InviteTenantMember),
            AuthoredIntentDigest.Compute(request), TenantInvitationTerms.ForMember(request.EmailAddress,
                request.Affiliation, request.Administrator, null, invitedBy), ct).ConfigureAwait(false);
    }
}
