using System.Globalization;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Native SQL directory; Portia owns the projection transaction and checkpoint.</summary>
sealed class CassieTeamRoleDirectoryReader(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, "TeamRoleDirectory"), ITeamRoleDirectoryProjection, ITeamRoleDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("TeamRoleDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamRoleAssigned assigned:
                CassieAccessControlSql.RequireScope(Identity, assigned.TenantId);
                await using (var insert = CreateCommand("INSERT INTO access_team_roles (row_key, projection_key, team_id, role_id, assignment_id, revision) VALUES (@key, @scope, @parent, @id, @assignment, @revision)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, assigned.TeamId.ToString(), assigned.RoleId.ToString())),
                        ("scope", CurrentProjectionKey), ("parent", assigned.TeamId.ToString()), ("id", assigned.RoleId.ToString()));
                    CassieAccessControlSql.Bind(insert, ("assignment", assigned.CommandId.ToString()),
                        ("revision", (assigned.ExpectedRevision + 1).ToString(CultureInfo.InvariantCulture)));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case TeamRoleRemoved removed:
                CassieAccessControlSql.RequireScope(Identity, removed.TenantId);
                await using (var delete = CreateCommand("DELETE FROM access_team_roles WHERE projection_key = @scope AND team_id = @parent AND role_id = @id"))
                {
                    CassieAccessControlSql.Bind(delete, ("scope", CurrentProjectionKey), ("parent", removed.TeamId.ToString()), ("id", removed.RoleId.ToString()));
                    _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
        }
    }

    public async ValueTask<Page<TeamRoleView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
        string? cursor, string? search, bool descending, CancellationToken ct = default)
    {
        var projectionKey = QueryKey(tenantId);
        var cursorScope = AccessControlDirectoryCursor.Scope(projectionKey, "access_team_roles", teamId.ToString(), search, descending);
        var after = AccessControlDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var fetchLimit = (take + 1).ToString(CultureInfo.InvariantCulture);
        var direction = descending ? "DESC" : "ASC";
        var comparison = descending ? "<" : ">";
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT team_id, role_id, assignment_id, revision FROM access_team_roles WHERE projection_key = @scope" +
            " AND team_id = @parent" +
            (after is null ? "" : $" AND role_id {comparison} @after") +
            $" ORDER BY role_id {direction}" + (search is null ? " LIMIT " + fetchLimit : ""));
        CassieAccessControlSql.Bind(command, ("scope", projectionKey));
        CassieAccessControlSql.Bind(command, ("parent", teamId.ToString()));
        if (after is { } position)
            CassieAccessControlSql.Bind(command, ("after", position.Sort));
        var matches = new List<TeamRoleView>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = new TeamRoleView(CassieAccessControlSql.Id(rows.GetString(0)), CassieAccessControlSql.Id(rows.GetString(1)), CassieAccessControlSql.Id(rows.GetString(2)), ulong.Parse(rows.GetString(3), CultureInfo.InvariantCulture));
            if (search is not null && !item.RoleId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
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
        return new Page<TeamRoleView>(matches, more ? AccessControlDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }
}
