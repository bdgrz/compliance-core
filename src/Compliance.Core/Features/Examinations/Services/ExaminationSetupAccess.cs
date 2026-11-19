using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>Checks current client membership before every first call and accepted retry.</summary>
public sealed class ExaminationSetupAccess(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions, TimeProvider clock)
{
    public async ValueTask<Result<ActorReference>> RequireAsync<T>(IRequestContext<T> context,
        Uuid tenantId, Uuid programId, string permission, CancellationToken ct) where T : IRequestBase
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Forbidden();
        var memberId = RbacIds.Member(tenantId, userId);
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
        if (member.UserId != userId || member.Affiliation != "client_personnel" ||
            !member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) ||
            !await permissions.IsAllowedAsync(tenantId, userId, memberId, programId, permission, ct).ConfigureAwait(false))
            return Forbidden();
        return Result<ActorReference>.Success(ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId)));
    }

    static Result<ActorReference> Forbidden() => Result<ActorReference>.Failure(new RequestError(
        RequestErrorKind.Forbidden, "This examination decision requires current authorized client membership."));
}
