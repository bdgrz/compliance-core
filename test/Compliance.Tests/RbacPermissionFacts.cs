using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests;

static class RbacPermissionFacts
{
    public static ActorAttribution Actor { get; } = new("portia:system", "Portia");

    public static Uuid AssignmentId(Uuid tenantId, Uuid roleId, string permission) =>
        Uuid.CreateVersion5(Uuid.UrlNamespace, $"test-permission:{tenantId}:{roleId}:{permission}");

    public static RolePermissionAssigned Assigned(Uuid tenantId, Uuid roleId, string permission) =>
        new(tenantId, roleId, permission, AssignmentId(tenantId, roleId, permission), 0, Actor);

    public static RolePermissionRemoved Removed(Uuid tenantId, Uuid roleId, string permission) =>
        new(tenantId, roleId, permission,
            Uuid.CreateVersion5(AssignmentId(tenantId, roleId, permission), "remove"), 1, Actor,
            AssignmentId(tenantId, roleId, permission));
    public static Task GrantAsync(IServiceProvider provider, Uuid tenantId, Uuid roleId, string permission) =>
        SetAsync(provider, tenantId, roleId, permission, assigned: true);

    public static Task RevokeAsync(IServiceProvider provider, Uuid tenantId, Uuid roleId, string permission) =>
        SetAsync(provider, tenantId, roleId, permission, assigned: false);

    static async Task SetAsync(IServiceProvider provider, Uuid tenantId, Uuid roleId, string permission, bool assigned)
    {
        await using var scope = provider.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new RolePermission(tenantId, roleId, permission));
        if (current.IsAssigned == assigned)
            return;
        var id = Uuid.CreateVersion4();
        var owner = new RolePermission(tenantId, roleId, permission, id);
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner,
            item => AggregateOutcome.CommitOnSuccess(assigned ? item.Assign(id, current.Version, Actor) :
                item.Remove(id, current.Version, current.AssignmentId!.Value, Actor)),
            new RequestDispatchContext(RequestActor.System, metadata: new RequestMetadata(id, id)));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
    public static void Hydrate(RolePermission permission, bool removed)
    {
        var assigned = DomainEventSeed.Attach(Assigned(permission.TenantId, permission.RoleId,
            permission.Permission), permission.Id, 1);
        var facts = removed ? new DomainEvent[] { assigned,
            DomainEventSeed.Attach(Removed(permission.TenantId, permission.RoleId, permission.Permission),
                permission.Id, 2) } : [assigned];
        _ = new AggregateScenario<RolePermission>(permission).Given(facts);
    }
}
