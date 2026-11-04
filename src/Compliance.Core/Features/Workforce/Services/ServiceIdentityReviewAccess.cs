using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Checks the current supported person link or current team membership from source owners.</summary>
public static class ServiceIdentityReviewAccess
{
    public static async ValueTask<bool> IsAllowedAsync(IAggregateReader reader, IPermissionAuthorizer permissions,
        Uuid tenantId, ServiceIdentity identity, Uuid userId, DateOnly today, CancellationToken ct)
    {
        var memberId = RbacIds.Member(tenantId, userId);
        if (await permissions.IsAllowedAsync(tenantId, userId, memberId, ServiceIdentityPermissions.Review, ct).ConfigureAwait(false))
            return true;
        if (!identity.IsCreated)
            return false;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
        if (!member.IsAccessEligibleOn(today) || member.Affiliation == "firm_staff" || member.UserId != userId)
            return false;
        if (identity.OwnerKind == "person")
        {
            var person = await reader.HydrateAsync(new Person(tenantId, identity.OwnerId), ct).ConfigureAwait(false);
            return person.IsCreated && person.CorrelatedUserId == userId;
        }
        if (identity.OwnerKind != "team")
            return false;
        var team = await reader.HydrateAsync(new Team(tenantId, identity.OwnerId), ct).ConfigureAwait(false);
        var assignment = await reader.HydrateAsync(new TeamMember(tenantId, identity.OwnerId, memberId), ct).ConfigureAwait(false);
        return team.IsActive && assignment.IsAssigned &&
               assignment.MembershipEpisodeId == member.MembershipEpisodeId;
    }
}
