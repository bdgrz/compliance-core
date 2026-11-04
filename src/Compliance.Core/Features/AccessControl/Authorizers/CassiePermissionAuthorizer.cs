using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Projectors;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Readers;

namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Stores permission graph and derived grants in Portia's native Cassie projection transaction.</summary>
sealed class CassiePermissionAuthorizer(CassieProjectionDatabase database, ITenantMembershipDirectoryReader memberships,
    IDomainEventReader events, IMemberAccessEligibility sourceMember)
    : CassieProjectionRepository(database, "PermissionProjection"), IPermissionProjection,
      IPermissionAuthorizer, IMemberAccessReader
{
    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("PermissionProjection",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var tenant = TenantOf(domainEvent);
        if (tenant is { } tenantId)
            CassieAccessControlSql.RequireScope(Identity, tenantId);
        PermissionProjectionState state;
        await using (var load = CreateCommand("SELECT CAST(payload AS TEXT) FROM access_permission_states WHERE projection_key = @scope"))
        {
            CassieAccessControlSql.Bind(load, ("scope", CurrentProjectionKey));
            var stored = await load.ExecuteScalarAsync(ct).ConfigureAwait(false);
            state = stored is string json
                ? JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.PermissionProjectionState) ?? new PermissionProjectionState()
                : new PermissionProjectionState();
        }
        if (state.TenantId != Uuid.Empty)
            CassieAccessControlSql.RequireScope(Identity, state.TenantId);
        state.Apply(domainEvent);
        await using (var delete = CreateCommand("DELETE FROM access_permission_grants WHERE projection_key = @scope"))
        {
            CassieAccessControlSql.Bind(delete, ("scope", CurrentProjectionKey));
            _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        foreach (var grant in state.Materialize())
        {
            await using var insert = CreateCommand("INSERT INTO access_permission_grants (row_key, projection_key, member_id, permission) VALUES (@key, @scope, @member, @permission) " +
                "ON CONFLICT (row_key) DO NOTHING");
            CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey,
                    grant.MemberId.ToString(), Permissions.Normalize(grant.Permission))),
                ("scope", CurrentProjectionKey), ("member", grant.MemberId.ToString()),
                ("permission", Permissions.Normalize(grant.Permission)));
            _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await using var save = CreateCommand("INSERT INTO access_permission_states (projection_key, payload) VALUES (@scope, CAST(@payload AS JSONB)) " +
            "ON CONFLICT (projection_key) DO UPDATE SET payload = EXCLUDED.payload");
        CassieAccessControlSql.Bind(save, ("scope", CurrentProjectionKey),
            ("payload", JsonSerializer.Serialize(state, ComplianceCoreJsonContext.Default.PermissionProjectionState)));
        _ = await save.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsAllowedAsync(
        Uuid tenantId,
        Uuid userId,
        Uuid memberId,
        string permission,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || string.IsNullOrWhiteSpace(permission))
            return false;
        permission = Permissions.Normalize(permission);

        // A stale grant key can predate the affiliation-aware projection. Check the current
        // membership before consulting that key so every direct consumer fails closed.
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not
            { Affiliation: "client_personnel", IsSuspended: false, IsDeprovisioned: false } ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return false;

        IReadOnlyList<MemberAccessEdge> currentAccess;
        await using (var initialRead = await OpenReadAsync(ct).ConfigureAwait(false))
        {
            if (!await HasGrantAsync(initialRead, tenantId, memberId, permission, ct).ConfigureAwait(false))
                return false;
            var state = await ReadStateAsync(initialRead, tenantId, ct).ConfigureAwait(false);
            currentAccess = state?.Explain(memberId)
                .Where(edge => BuiltInRbac.IsBuiltInRole(tenantId, edge.RoleId)).ToArray() ?? [];
        }

        // Older projections can retain a grant key without an explainable active path.
        // Such a key must never authorize an action, especially while a suspension event
        // is waiting behind the membership and permission projection checkpoints.
        if (!currentAccess.Any(edge => edge.Permissions.Contains(permission, StringComparer.Ordinal)))
            return false;
        if (await HasPendingRevocationAsync(tenantId, memberId, permission, currentAccess, ct)
                .ConfigureAwait(false))
            return false;

        // The projector may have caught up while pending events were scanned. Re-read the
        // materialized key so a completed revocation cannot race past that checkpoint.
        bool currentGrantFound;
        await using (var finalRead = await OpenReadAsync(ct).ConfigureAwait(false))
        {
            currentGrantFound = await HasGrantAsync(finalRead, tenantId, memberId, permission, ct).ConfigureAwait(false);
        }
        // A suspension can commit after the pending-event scan and before this final key read.
        // Hydrate the tenant-addressed member stream at the last positive decision point.
        return currentGrantFound &&
            await sourceMember.IsEligibleAsync(tenantId, userId, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> HasPendingRevocationAsync(Uuid tenantId, Uuid memberId,
        string permission, IReadOnlyList<MemberAccessEdge> currentAccess, CancellationToken ct)
    {
        var remainingPaths = currentAccess.Where(edge => edge.Permissions.Contains(permission,
            StringComparer.Ordinal)).ToList();
        if (remainingPaths.Count == 0)
            return false;

        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "PermissionProjection", pattern), ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        while (await pending.MoveNextAsync().ConfigureAwait(false))
        {
            var domainEvent = pending.Current.Event;
            if (PermissionProjector.RevokesAllMemberAccess(domainEvent, memberId))
                return true;
            remainingPaths.RemoveAll(edge => PermissionProjector.RevokesAccessPath(domainEvent,
                memberId, permission, edge));
            if (remainingPaths.Count == 0)
                return true;
        }
        return false;
    }

    public async ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        var state = await ReadStateAsync(read, tenantId, ct).ConfigureAwait(false);
        return state?.Explain(memberId) ?? [];
    }

    async ValueTask<PermissionProjectionState?> ReadStateAsync(CassieProjectionReadSession read,
        Uuid tenantId, CancellationToken ct)
    {
        await using var command = read.CreateCommand("SELECT CAST(payload AS TEXT) FROM access_permission_states WHERE projection_key = @scope");
        CassieAccessControlSql.Bind(command, ("scope", QueryKey(tenantId)));
        var stored = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (stored is not string json)
            return null;
        var state = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.PermissionProjectionState);
        return state?.TenantId == tenantId ? state : null;
    }

    async ValueTask<bool> HasGrantAsync(CassieProjectionReadSession read, Uuid tenantId,
        Uuid memberId, string permission, CancellationToken ct)
    {
        await using var command = read.CreateCommand("SELECT member_id FROM access_permission_grants WHERE row_key = @key AND projection_key = @scope AND member_id = @member AND permission = @permission");
        var scope = QueryKey(tenantId);
        CassieAccessControlSql.Bind(command, ("key", CassieAccessControlSql.RowKey(scope, memberId.ToString(), permission)),
            ("scope", scope), ("member", memberId.ToString()), ("permission", permission));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is string;
    }

    static Uuid? TenantOf(DomainEvent domainEvent) => domainEvent switch
    {
        MemberRegistered ev => ev.TenantId,
        MemberSuspended ev => ev.TenantId,
        MemberReinstated ev => ev.TenantId,
        MemberDeprovisioned ev => ev.TenantId,
        TeamDefined ev => ev.TenantId,
        TeamDeleted ev => ev.TenantId,
        TeamMemberAssigned ev => ev.TenantId,
        TeamMemberRemoved ev => ev.TenantId,
        RoleDefined ev => ev.TenantId,
        RoleRenamed ev => ev.TenantId,
        RoleDeleted ev => ev.TenantId,
        RolePermissionAssigned ev => ev.TenantId,
        RolePermissionRemoved ev => ev.TenantId,
        TeamRoleAssigned ev => ev.TenantId,
        TeamRoleRemoved ev => ev.TenantId,
        _ => null,
    };
}
