using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Explains current team-derived access; historical direct grants confer no permissions.</summary>
public sealed class GetTeamMemberAccessHandler(IAggregateReader reader, IMemberAccessReader access,
    ITeamDirectoryReader teams, IRoleDirectoryReader roles)
    : IRequestHandler<GetMemberAccess, MemberAccessView>
{
    public async ValueTask<Result<MemberAccessView>> HandleAsync(IRequestContext<GetMemberAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct).ConfigureAwait(false);
        if (!member.IsRegistered)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.NotFound, "The member was not found."));
        if (request.ExpectedBuiltInRole is { } requested && BuiltInRbac.RoleIdForRole(request.TenantId, requested) is null)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Validation, "The expected built-in role is not supported."));
        var edges = await access.ReadAsync(request.TenantId, member.Id, ct).ConfigureAwait(false);
        if (request.ExpectedBuiltInRole is { } role && BuiltInRbac.RoleIdForRole(request.TenantId, role) is { } expected &&
            !edges.Any(edge => edge.RoleId == expected))
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict, "The expected role is not currently assigned."));
        var paths = new List<MemberAccessPath>();
        foreach (var edge in edges.Where(edge => edge.TeamId != Uuid.Empty))
        {
            var team = await reader.HydrateAsync(new Team(request.TenantId, edge.TeamId), ct).ConfigureAwait(false);
            var sourceRole = await reader.HydrateAsync(new Role(request.TenantId, edge.RoleId), ct).ConfigureAwait(false);
            if (team.IsActive && sourceRole.IsActive)
            {
                var teamView = await teams.GetAsync(request.TenantId, edge.TeamId, ct).ConfigureAwait(false);
                var roleView = await roles.GetAsync(request.TenantId, edge.RoleId, ct).ConfigureAwait(false);
                if (teamView is null || roleView is null)
                    return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The access explanation projection is still catching up.", isTransient: true));
                paths.Add(new MemberAccessPath(edge.TeamId, teamView.Name, edge.RoleId, roleView.Name, edge.Permissions, edge.Scope));
            }
        }
        var permissions = edges.Where(path => TeamRoleScopes.Normalize(request.TenantId, path.Scope).Kind == AccessGrantScopeKind.Organization)
            .SelectMany(path => path.Permissions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return Result<MemberAccessView>.Success(new MemberAccessView(request.TenantId, request.UserId, member.Id, paths, [], permissions,
            edges.Any(edge => edge.TeamId == Uuid.Empty) ? member.PrimaryDesignationId : Uuid.Empty));
    }
}
