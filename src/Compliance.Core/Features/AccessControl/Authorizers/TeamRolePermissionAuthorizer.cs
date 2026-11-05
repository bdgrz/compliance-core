using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>
/// Evaluates team-role authority from current owners. Projection rows and legacy direct grants
/// never confer authority. A guest's membership programs cap every team-derived permission.
/// </summary>
sealed class TeamRolePermissionAuthorizer(IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IPermissionAuthorizer, IAccessGrantPermissionAuthorizer, IAccessGrantScopePermissionAuthorizer,
      IAccessGrantRolePermissionAuthorizer, IMemberAccessReader
{
    public async ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        string permission, CancellationToken ct = default) =>
        (await ScopesAsync(tenantId, userId, memberId, permission, null, ct).ConfigureAwait(false))
        .Any(scope => scope.Kind == AccessGrantScopeKind.Organization);

    public async ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        Uuid programId, string permission, CancellationToken ct = default)
    {
        if (programId == Uuid.Empty)
            return false;
        var visibility = await GetProgramVisibilityAsync(tenantId, userId, memberId, permission, ct).ConfigureAwait(false);
        return visibility.OrganizationWide || visibility.ProgramIds.Contains(programId);
    }

    public async ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
    {
        var scopes = await ScopesAsync(tenantId, userId, memberId, permission, null, ct).ConfigureAwait(false);
        return new ProgramAccessVisibility(scopes.Any(scope => scope.Kind == AccessGrantScopeKind.Organization),
            scopes.Where(scope => scope.Kind == AccessGrantScopeKind.Program).Select(scope => scope.Id).ToHashSet());
    }

    public async ValueTask<bool> IsAllowedAsOrganizationRoleAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, Uuid roleId, string permission, CancellationToken ct = default) => roleId != Uuid.Empty &&
        (await ScopesAsync(tenantId, userId, memberId, permission, roleId, ct).ConfigureAwait(false))
        .Any(scope => scope.Kind == AccessGrantScopeKind.Organization);

    public async ValueTask<bool> IsAllowedAsRoleAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, Uuid programId, Uuid roleId, string permission, CancellationToken ct = default) =>
        programId != Uuid.Empty && roleId != Uuid.Empty &&
        (await ScopesAsync(tenantId, userId, memberId, permission, roleId, ct).ConfigureAwait(false))
        .Any(scope => scope.Kind == AccessGrantScopeKind.Organization ||
            scope.Kind == AccessGrantScopeKind.Program && scope.Id == programId);

    public async ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
        CancellationToken ct = default)
    {
        if (scopes is null || scopes.Count == 0)
            return false;
        var requested = scopes.Where(scope => scope is not null && scope.Id != Uuid.Empty &&
            Enum.IsDefined(scope.Kind) &&
            (scope.Kind != AccessGrantScopeKind.Organization || scope.Id == tenantId))
            .Select(scope => scope.Kind switch
            {
                AccessGrantScopeKind.Application => new AccessGrantScope(AccessGrantScopeKind.SharedResource, scope.Id, "application"),
                AccessGrantScopeKind.SystemInstance => new AccessGrantScope(AccessGrantScopeKind.SharedResource, scope.Id, "system_instance"),
                _ => scope,
            }).ToHashSet();
        if (requested.Count == 0)
            return false;
        return (await ScopesAsync(tenantId, userId, memberId, permission, null, ct).ConfigureAwait(false))
            .Any(scope => scope.Kind == AccessGrantScopeKind.Organization || requested.Contains(scope));
    }

    public async ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
        (await ScopesAsync(tenantId, userId, memberId, permission, null, ct).ConfigureAwait(false))
        .Any(scope => scope.Kind is AccessGrantScopeKind.Organization ||
            scope.Kind == AccessGrantScopeKind.SharedResource && scope.ResourceType is "application" or "system_instance");

    public async ValueTask<bool> IsAllowedAtAnyTechnologyInventoryScopeAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
        (await ScopesAsync(tenantId, userId, memberId, permission, null, ct).ConfigureAwait(false))
        .Any(scope => scope.Kind is AccessGrantScopeKind.Organization ||
            scope.Kind == AccessGrantScopeKind.SharedResource && scope.ResourceType is
                TechnologyInventoryResourceTypes.InformationAsset or TechnologyInventoryResourceTypes.DataFlow);

    public async ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty)
            return [];
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
        return await EdgesAsync(tenantId, member.UserId, memberId, null, null, ct).ConfigureAwait(false);
    }

    async ValueTask<IReadOnlySet<AccessGrantScope>> ScopesAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, string permission, Uuid? requiredRole, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return new HashSet<AccessGrantScope>();
        return (await EdgesAsync(tenantId, userId, memberId, permission, requiredRole, ct).ConfigureAwait(false))
            .Select(edge => TeamRoleScopes.Normalize(tenantId, edge.Scope)).ToHashSet();
    }

    async ValueTask<IReadOnlyList<MemberAccessEdge>> EdgesAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, string? permission, Uuid? requiredRole, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty || memberId != RbacIds.Member(tenantId, userId))
            return [];
        if (permission is not null)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return [];
            permission = Permissions.Normalize(permission);
            if (!ProductPermissionCatalog.Contains(permission))
                return [];
        }
        var member = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        if (!Eligible(member) || !(await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false)).IsActive)
            return [];

        var permitted = new List<(MemberAccessEdge Edge, Uuid AssignmentId)>();
        foreach (var candidate in await AssignmentsAsync(tenantId, ct).ConfigureAwait(false))
        {
            if (requiredRole is { } roleId && candidate.RoleId != roleId ||
                candidate.RoleId == BuiltInRbac.AdvisorRoleId(tenantId) || candidate.RoleId == BuiltInRbac.AttestRoleId(tenantId))
                continue;
            var scope = TeamRoleScopes.Normalize(tenantId, candidate.Scope);
            if (!TeamRoleScopes.IsValid(tenantId, scope))
                continue;
            var assignment = await reader.HydrateAsync(new TeamRole(tenantId, candidate.TeamId,
                candidate.RoleId, scope: scope), ct).ConfigureAwait(false);
            if (!assignment.IsAssigned || assignment.AssignmentId != candidate.CommandId)
                continue;
            var teamMember = await reader.HydrateAsync(new TeamMember(tenantId, candidate.TeamId, memberId), ct).ConfigureAwait(false);
            if (!teamMember.IsAssigned || teamMember.MembershipEpisodeId != member.MembershipEpisodeId)
                continue;
            if (!(await reader.HydrateAsync(new Team(tenantId, candidate.TeamId), ct).ConfigureAwait(false)).IsActive ||
                !(await reader.HydrateAsync(new Role(tenantId, candidate.RoleId), ct).ConfigureAwait(false)).IsActive)
                continue;
            var names = permission is not null ? new[] { permission } :
                (await RbacRelationshipSource.LivePermissionsAsync(events, tenantId, candidate.RoleId, ct).ConfigureAwait(false))
                .Select(item => item.Permission).Where(ProductPermissionCatalog.Contains).ToArray();
            var currentPermissions = new List<string>();
            foreach (var name in names)
            {
                if ((await reader.HydrateAsync(new RolePermission(tenantId, candidate.RoleId, name), ct).ConfigureAwait(false)).IsAssigned)
                    currentPermissions.Add(name);
            }
            if (currentPermissions.Count != 0)
                permitted.Add((new MemberAccessEdge(candidate.TeamId, candidate.RoleId, currentPermissions, scope), candidate.CommandId));
        }

        // A revocation may commit during a permission read, after the initial team/role checks.
        // Revalidate the selected paths from their owners, including assignment and membership episodes.
        var verified = new List<MemberAccessEdge>();
        foreach (var (edge, assignmentId) in permitted)
        {
            var assignment = await reader.HydrateAsync(new TeamRole(tenantId, edge.TeamId, edge.RoleId, scope: edge.Scope), ct).ConfigureAwait(false);
            var teamMember = await reader.HydrateAsync(new TeamMember(tenantId, edge.TeamId, memberId), ct).ConfigureAwait(false);
            if (!assignment.IsAssigned || assignment.AssignmentId != assignmentId ||
                !teamMember.IsAssigned || teamMember.MembershipEpisodeId != member.MembershipEpisodeId ||
                !(await reader.HydrateAsync(new Team(tenantId, edge.TeamId), ct).ConfigureAwait(false)).IsActive ||
                !(await reader.HydrateAsync(new Role(tenantId, edge.RoleId), ct).ConfigureAwait(false)).IsActive)
                continue;
            var retained = new List<string>();
            foreach (var name in edge.Permissions)
            {
                if ((await reader.HydrateAsync(new RolePermission(tenantId, edge.RoleId, name), ct).ConfigureAwait(false)).IsAssigned)
                    retained.Add(name);
            }
            if (retained.Count != 0)
                verified.Add(edge with { Permissions = retained });
        }

        // Membership is read again after relationships: a suspension or new invitation episode
        // cannot retain a path admitted for the earlier episode. Guest limits come from this owner.
        var current = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        if (!Eligible(current) || current.MembershipEpisodeId != member.MembershipEpisodeId ||
            !(await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false)).IsActive)
            return [];
        if (current.Affiliation != "guest")
            return verified;
        return verified.SelectMany(edge => current.GuestProgramIds.Where(program =>
                edge.Scope!.Kind == AccessGrantScopeKind.Organization ||
                edge.Scope.Kind == AccessGrantScopeKind.Program && edge.Scope.Id == program)
            .Select(program => edge with { Scope = new AccessGrantScope(AccessGrantScopeKind.Program, program) })).ToArray();
    }

    bool Eligible(Member member) => member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

    async ValueTask<IReadOnlyList<TeamRoleAssigned>> AssignmentsAsync(Uuid tenantId, CancellationToken ct)
    {
        var live = new Dictionary<EventStreamAddress, TeamRoleAssigned>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-team-roles");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamRoleAssigned assigned when assigned.TenantId == tenantId:
                    if (record.Stream != new TeamRole(tenantId, assigned.TeamId, assigned.RoleId, scope: assigned.Scope).Stream)
                        throw new InvalidOperationException("A team-role fact must belong to its canonical scoped owner.");
                    live[record.Stream] = assigned;
                    break;
                case TeamRoleRemoved:
                    live.Remove(record.Stream);
                    break;
            }
        }
        return live.Values.ToArray();
    }
}
