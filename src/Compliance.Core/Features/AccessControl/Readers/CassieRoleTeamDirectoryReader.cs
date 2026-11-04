using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Native SQL directory; Portia owns the projection transaction and checkpoint.</summary>
sealed class CassieRoleTeamDirectoryReader(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, "RoleTeamDirectory"), IRoleTeamDirectoryProjection, IRoleTeamDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("RoleTeamDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamRoleAssigned assigned:
                CassieAccessControlSql.RequireScope(Identity, assigned.TenantId);
                var scope = TeamRoleScopes.Normalize(assigned.TenantId, assigned.Scope);
                var scopeKey = TeamRoleScopes.Key(scope);
                await using (var insert = CreateCommand("INSERT INTO access_role_teams (row_key, projection_key, role_id, team_id, assignment_id, revision, scope_payload, sort_key) VALUES (@key, @scope, @parent, @id, @assignment, @revision, @payload, @sort)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, assigned.RoleId.ToString(), assigned.TeamId.ToString(), scopeKey)),
                        ("scope", CurrentProjectionKey), ("parent", assigned.RoleId.ToString()), ("id", assigned.TeamId.ToString()));
                    CassieAccessControlSql.Bind(insert, ("assignment", assigned.CommandId.ToString()),
                        ("revision", (assigned.ExpectedRevision + 1).ToString(CultureInfo.InvariantCulture)));
                    CassieAccessControlSql.Bind(insert, ("payload", JsonSerializer.Serialize(scope, ComplianceCoreJsonContext.Default.AccessGrantScope)),
                        ("sort", assigned.TeamId.ToString() + ":" + scopeKey));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case TeamRoleRemoved removed:
                CassieAccessControlSql.RequireScope(Identity, removed.TenantId);
                await using (var delete = CreateCommand("DELETE FROM access_role_teams WHERE row_key = @key AND projection_key = @scope AND role_id = @parent AND team_id = @id"))
                {
                    CassieAccessControlSql.Bind(delete, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, removed.RoleId.ToString(),
                            removed.TeamId.ToString(), TeamRoleScopes.Key(TeamRoleScopes.Normalize(removed.TenantId, removed.Scope)))),
                        ("scope", CurrentProjectionKey), ("parent", removed.RoleId.ToString()), ("id", removed.TeamId.ToString()));
                    _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
        }
    }

    public async ValueTask<Page<RoleTeamView>> ListAsync(Uuid tenantId, Uuid roleId, int? limit,
        string? cursor, string? search, bool descending, CancellationToken ct = default)
    {
        var projectionKey = QueryKey(tenantId);
        var cursorScope = AccessControlDirectoryCursor.Scope(projectionKey, "access_role_teams", roleId.ToString(), search, descending);
        var after = AccessControlDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var fetchLimit = (take + 1).ToString(CultureInfo.InvariantCulture);
        var direction = descending ? "DESC" : "ASC";
        var comparison = descending ? "<" : ">";
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT role_id, team_id, assignment_id, revision, scope_payload, sort_key FROM access_role_teams WHERE projection_key = @scope" +
            " AND role_id = @parent" +
            (after is null ? "" : $" AND sort_key {comparison} @after") +
            $" ORDER BY sort_key {direction}" + (search is null ? " LIMIT " + fetchLimit : ""));
        CassieAccessControlSql.Bind(command, ("scope", projectionKey));
        CassieAccessControlSql.Bind(command, ("parent", roleId.ToString()));
        if (after is { } position)
            CassieAccessControlSql.Bind(command, ("after", position.Sort));
        var matches = new List<RoleTeamView>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = new RoleTeamView(CassieAccessControlSql.Id(rows.GetString(0)), CassieAccessControlSql.Id(rows.GetString(1)), CassieAccessControlSql.Id(rows.GetString(2)), ulong.Parse(rows.GetString(3), CultureInfo.InvariantCulture),
                JsonSerializer.Deserialize(rows.GetString(4), ComplianceCoreJsonContext.Default.AccessGrantScope));
            if (search is not null && !item.TeamId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (matches.Count == take)
            {
                more = true;
                break;
            }
            matches.Add(item);
            lastSort = rows.GetString(5);
            lastId = rows.GetString(5);
        }
        return new Page<RoleTeamView>(matches, more ? AccessControlDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }
}
