using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks shared by the guard-first administrator routes.</summary>
static class AdministratorAdmissions
{
    /// <summary>The error a route reports while another change to the same source owner of the episode is unfinished.</summary>
    public static RequestError InProgress { get; } = new(RequestErrorKind.Conflict,
        "Another change to this member is in progress.", true);

    /// <summary>The acting member of a Bdgrz user, as the durable actor of the admitted operation.</summary>
    public static ActorReference? MemberActor(ClaimsPrincipal actor, Uuid tenantId)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return null;
        return ActorReference.ForMember(RbacIds.Member(tenantId, userId), UserIdentityClaims.BdgrzDisplay(actor, userId));
    }

    /// <summary>
    ///     Fails transiently while an unfinished operation of the episode lands on the same source owner and the new
    ///     one cannot wait behind it. <paramref name="allowedBehind" /> names the pending operations it may follow.
    /// </summary>
    public static Result Unfinished(TenantManagerGuard guard, Uuid episode, bool memberOwned,
        Func<PendingTransition, bool> allowedBehind) =>
        guard.PendingFor(episode).Any(pending =>
            (memberOwned ? pending.OwnedByMember : pending.OwnedByAssignment) && !allowedBehind(pending))
            ? Result.Failure(InProgress)
            : Result.Success;
}
