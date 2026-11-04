using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Member-link decisions require a current Org Admin path, never the broader workforce permission.</summary>
public sealed class PersonMemberLinkAccess(IAggregateReader reader, IMemberAccessReader access,
    IPermissionAuthorizer permissions, TimeProvider clock)
{
    public async ValueTask<Result> RequireAsync<T>(IRequestContext<T> context, Uuid tenant, CancellationToken ct)
        where T : IRequestBase
    {
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Denied();
        var member = await reader.HydrateAsync(new Member(tenant, user), ct).ConfigureAwait(false);
        if (member.Affiliation != "client_personnel" || !member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)))
            return Denied();
        var role = BuiltInRbac.TenantAdministrationRoleId(tenant);
        var edges = await access.ReadAsync(tenant, member.Id, ct).ConfigureAwait(false);
        foreach (var edge in edges.Where(value => value.RoleId == role))
        {
            var teamMember = await reader.HydrateAsync(new TeamMember(tenant, edge.TeamId, member.Id), ct).ConfigureAwait(false);
            var teamRole = await reader.HydrateAsync(new TeamRole(tenant, edge.TeamId, role), ct).ConfigureAwait(false);
            if (teamMember.IsAssigned && teamMember.MembershipEpisodeId == member.MembershipEpisodeId && teamRole.IsAssigned &&
                await permissions.IsAllowedAsync(tenant, user, member.Id, RbacPermissions.TenantRbacManage, ct).ConfigureAwait(false))
                return Result.Success;
        }
        return Denied();
    }

    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only a current Org Admin may link or unlink workforce members."));
}
