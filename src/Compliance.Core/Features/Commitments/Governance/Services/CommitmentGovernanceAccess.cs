using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Services;

public sealed class CommitmentGovernanceAccess(IAggregateReader reader, IPermissionAuthorizer permissions, TimeProvider clock)
{
    public async ValueTask<Result<ActorReference>> RequireAsync(IRequestContext context, Uuid tenant, string permission,
        CancellationToken ct)
    {
        if (tenant == Uuid.Empty || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Denied();
        var memberId = RbacIds.Member(tenant, user);
        var member = await reader.HydrateAsync(Member.ForVerification(tenant, memberId), ct).ConfigureAwait(false);
        if (!member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) || member.UserId != user ||
            member.Affiliation == "firm_staff" || !await permissions.IsAllowedAsync(tenant, user, memberId, permission, ct).ConfigureAwait(false))
            return Denied();
        return Result<ActorReference>.Success(ActorReference.ForMember(memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, user)));
    }
    static Result<ActorReference> Denied() => Result<ActorReference>.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Current client membership and the commitment permission are required."));
}
