using System.Globalization;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Native SQL directory; Portia owns the projection transaction and checkpoint.</summary>
sealed class CassieRoleDirectoryReader(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, "RoleDirectory"), IRoleDirectoryProjection, IRoleDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("RoleDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RoleDefined defined:
                CassieAccessControlSql.RequireScope(Identity, defined.TenantId);
                await using (var insert = CreateCommand("INSERT INTO access_roles (row_key, projection_key, role_id, name, sort_name) VALUES (@key, @scope, @id, @name, @sort)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, defined.RoleId.ToString())),
                        ("scope", CurrentProjectionKey), ("id", defined.RoleId.ToString()),
                        ("name", defined.Name), ("sort", defined.Name.ToUpperInvariant()));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case RoleRenamed renamed:
                CassieAccessControlSql.RequireScope(Identity, renamed.TenantId);
                await DeleteAsync(renamed.RoleId, ct).ConfigureAwait(false);
                await using (var insert = CreateCommand("INSERT INTO access_roles (row_key, projection_key, role_id, name, sort_name) VALUES (@key, @scope, @id, @name, @sort)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, renamed.RoleId.ToString())),
                        ("scope", CurrentProjectionKey), ("id", renamed.RoleId.ToString()),
                        ("name", renamed.Name), ("sort", renamed.Name.ToUpperInvariant()));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case RoleDeleted deleted:
                CassieAccessControlSql.RequireScope(Identity, deleted.TenantId);
                await DeleteAsync(deleted.RoleId, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask DeleteAsync(Uuid id, CancellationToken ct)
    {
        await using var command = CreateCommand("DELETE FROM access_roles WHERE row_key = @key AND projection_key = @scope AND role_id = @id");
        CassieAccessControlSql.Bind(command, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, id.ToString())),
            ("scope", CurrentProjectionKey), ("id", id.ToString()));
        _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct = default)
    {
        var scope = QueryKey(tenantId);
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT role_id, name FROM access_roles WHERE row_key = @key AND projection_key = @scope AND role_id = @id");
        CassieAccessControlSql.Bind(command, ("key", CassieAccessControlSql.RowKey(scope, roleId.ToString())),
            ("scope", scope), ("id", roleId.ToString()));
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rows.ReadAsync(ct).ConfigureAwait(false) ? new RoleView(CassieAccessControlSql.Id(rows.GetString(0)), rows.GetString(1)) : null;
    }

    public async ValueTask<Page<RoleView>> ListAsync(Uuid tenantId, int? limit,
        string? cursor, string? search, bool descending, CancellationToken ct = default)
    {
        var projectionKey = QueryKey(tenantId);
        var cursorScope = AccessControlDirectoryCursor.Scope(projectionKey, "access_roles", "", search, descending);
        var after = AccessControlDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var fetchLimit = (take + 1).ToString(CultureInfo.InvariantCulture);
        var direction = descending ? "DESC" : "ASC";
        var comparison = descending ? "<" : ">";
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT role_id, name, sort_name FROM access_roles WHERE projection_key = @scope" +
            (after is null ? "" : $" AND (sort_name {comparison} @after OR (sort_name = @after AND role_id {comparison} @after_id))") +
            $" ORDER BY sort_name {direction}, role_id {direction}" + (search is null ? " LIMIT " + fetchLimit : ""));
        CassieAccessControlSql.Bind(command, ("scope", projectionKey));
        if (after is { } position)
            CassieAccessControlSql.Bind(command, ("after", position.Sort), ("after_id", position.Id));
        var matches = new List<RoleView>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = new RoleView(CassieAccessControlSql.Id(rows.GetString(0)), rows.GetString(1));
            if (search is not null && !item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (matches.Count == take)
            {
                more = true;
                break;
            }
            matches.Add(item);
            lastSort = rows.GetString(2);
            lastId = rows.GetString(0);
        }
        return new Page<RoleView>(matches, more ? AccessControlDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }
}
