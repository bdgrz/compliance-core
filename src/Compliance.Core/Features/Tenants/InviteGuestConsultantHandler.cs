using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteGuestConsultantHandler(TenantInvitationIssuer issuer,
    IProgramResourceScopeResolver programScopes) : IRequestHandler<InviteGuestConsultant>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<InviteGuestConsultant> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var invitedBy))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Guest invitations require a Bdgrz user identity."));
        if (request.TenantId == Uuid.Empty || request.ProgramIds is not { Count: > 0 } programIds ||
            programIds.Any(id => id == Uuid.Empty) ||
            programIds.Distinct().Count() != programIds.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A guest invitation requires distinct program scopes in one organization."));

        foreach (var programId in programIds)
        {
            if (!await programScopes.IsTenantProgramAsync(request.TenantId, programId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "A guest invitation program was not found in this organization."));
        }

        return await issuer.IssueGuestAsync(context, request.TenantId, request.EmailAddress,
            programIds, request.ExpiresOn, invitedBy,
            UserIdentityClaims.BdgrzDisplay(context.Actor, invitedBy), ct).ConfigureAwait(false);
    }
}
