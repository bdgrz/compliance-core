using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests;

static class RbacTeamRoleFacts
{
    public static ActorAttribution Actor { get; } = new("portia:system", "Portia");
    public static Uuid AssignmentId(Uuid tenantId, Uuid teamId, Uuid roleId) =>
        Uuid.CreateVersion5(Uuid.UrlNamespace, $"test-team-role:{tenantId}:{teamId}:{roleId}");
    public static TeamRoleAssigned Assigned(Uuid tenantId, Uuid teamId, Uuid roleId) =>
        new(tenantId, teamId, roleId, AssignmentId(tenantId, teamId, roleId), 0, Actor);
    public static TeamRoleRemoved Removed(Uuid tenantId, Uuid teamId, Uuid roleId) =>
        new(tenantId, teamId, roleId, Uuid.CreateVersion5(AssignmentId(tenantId, teamId, roleId), "remove"),
            1, Actor, AssignmentId(tenantId, teamId, roleId));

    public static async Task GrantAsync(IServiceProvider provider, Uuid tenantId, Uuid teamId, Uuid roleId)
    {
        await using var scope = provider.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(tenantId, teamId, roleId));
        if (current.IsAssigned)
            return;
        var id = Uuid.CreateVersion4();
        var owner = new TeamRole(tenantId, teamId, roleId, id);
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            item => AggregateOutcome.CommitOnSuccess(item.Assign(id, current.Version, Actor)),
            new RequestDispatchContext(RequestActor.System, metadata: new RequestMetadata(id, id)));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public static async Task RevokeAsync(IServiceProvider provider, Uuid tenantId, Uuid teamId, Uuid roleId)
    {
        await using var scope = provider.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new TeamRole(tenantId, teamId, roleId));
        if (!current.IsAssigned)
            return;
        var id = Uuid.CreateVersion4();
        var owner = new TeamRole(tenantId, teamId, roleId, id);
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            item => AggregateOutcome.CommitOnSuccess(item.Remove(id, current.Version, current.AssignmentId!.Value, Actor)),
            new RequestDispatchContext(RequestActor.System, metadata: new RequestMetadata(id, id)));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public static void Hydrate(TeamRole owner, bool removed)
    {
        var assigned = DomainEventSeed.Attach(Assigned(owner.TenantId, owner.TeamId, owner.RoleId), owner.Id, 1);
        var facts = removed ? new DomainEvent[] { assigned,
            DomainEventSeed.Attach(Removed(owner.TenantId, owner.TeamId, owner.RoleId), owner.Id, 2) } : [assigned];
        _ = new AggregateScenario<TeamRole>(owner).Given(facts);
    }
}
