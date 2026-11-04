using System.Globalization;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Native SQL directory; Portia owns the projection transaction and checkpoint.</summary>
sealed class CassieRolePermissionDirectoryReader(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, "RolePermissionDirectory"), IRolePermissionDirectoryProjection, IRolePermissionDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("RolePermissionDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RolePermissionAssigned assigned:
                CassieAccessControlSql.RequireScope(Identity, assigned.TenantId);
                await using (var insert = CreateCommand("INSERT INTO access_role_permissions (row_key, projection_key, role_id, permission, assignment_id, revision) VALUES (@key, @scope, @parent, @id, @assignment, @revision)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, assigned.RoleId.ToString(), assigned.Permission)),
                        ("scope", CurrentProjectionKey), ("parent", assigned.RoleId.ToString()), ("id", assigned.Permission));
                    CassieAccessControlSql.Bind(insert, ("assignment", assigned.CommandId.ToString()),
                        ("revision", (assigned.ExpectedRevision + 1).ToString(CultureInfo.InvariantCulture)));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case RolePermissionRemoved removed:
                CassieAccessControlSql.RequireScope(Identity, removed.TenantId);
                await using (var delete = CreateCommand("DELETE FROM access_role_permissions WHERE projection_key = @scope AND role_id = @parent AND permission = @id"))
                {
                    CassieAccessControlSql.Bind(delete, ("scope", CurrentProjectionKey), ("parent", removed.RoleId.ToString()), ("id", removed.Permission));
                    _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
        }
    }

    public async ValueTask<Page<RolePermissionView>> ListAsync(Uuid tenantId, Uuid roleId, int? limit,
        string? cursor, string? search, bool descending, CancellationToken ct = default)
    {
        var projectionKey = QueryKey(tenantId);
        var cursorScope = AccessControlDirectoryCursor.Scope(projectionKey, "access_role_permissions", roleId.ToString(), search, descending);
        var after = AccessControlDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var direction = descending ? "DESC" : "ASC";
        var comparison = descending ? "<" : ">";
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT role_id, permission, assignment_id, revision FROM access_role_permissions WHERE projection_key = @scope" +
            " AND role_id = @parent" +
            (after is null ? "" : $" AND permission {comparison} @after") +
            $" ORDER BY permission {direction}" + (search is null ? " LIMIT @take" : ""));
        CassieAccessControlSql.Bind(command, ("scope", projectionKey));
        CassieAccessControlSql.Bind(command, ("parent", roleId.ToString()));
        if (search is null)
            CassieAccessControlSql.Bind(command, ("take", take + 1));
        if (after is { } position)
            CassieAccessControlSql.Bind(command, ("after", position.Sort));
        var matches = new List<RolePermissionView>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = new RolePermissionView(CassieAccessControlSql.Id(rows.GetString(0)), rows.GetString(1), CassieAccessControlSql.Id(rows.GetString(2)), ulong.Parse(rows.GetString(3), CultureInfo.InvariantCulture));
            if (search is not null && !item.Permission.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (matches.Count == take)
            {
                more = true;
                break;
            }
            matches.Add(item);
            lastSort = rows.GetString(1);
            lastId = rows.GetString(1);
        }
        return new Page<RolePermissionView>(matches, more ? AccessControlDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }
}
