using System.Globalization;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Native SQL directory; Portia owns the projection transaction and checkpoint.</summary>
sealed class CassieTeamMemberDirectoryReader(CassieProjectionDatabase database, IDomainEventReader events)
    : CassieProjectionRepository(database, "TeamMemberDirectory"), ITeamMemberDirectoryProjection, ITeamMemberDirectoryReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    string QueryKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity("TeamMemberDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString())));

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamMemberAssigned assigned:
                CassieAccessControlSql.RequireScope(Identity, assigned.TenantId);
                await using (var insert = CreateCommand("INSERT INTO access_team_members (row_key, projection_key, team_id, member_id) VALUES (@key, @scope, @parent, @id)"))
                {
                    CassieAccessControlSql.Bind(insert, ("key", CassieAccessControlSql.RowKey(CurrentProjectionKey, assigned.TeamId.ToString(), assigned.MemberId.ToString())),
                        ("scope", CurrentProjectionKey), ("parent", assigned.TeamId.ToString()), ("id", assigned.MemberId.ToString()));
                    _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
            case TeamMemberRemoved removed:
                CassieAccessControlSql.RequireScope(Identity, removed.TenantId);
                await using (var delete = CreateCommand("DELETE FROM access_team_members WHERE projection_key = @scope AND team_id = @parent AND member_id = @id"))
                {
                    CassieAccessControlSql.Bind(delete, ("scope", CurrentProjectionKey), ("parent", removed.TeamId.ToString()), ("id", removed.MemberId.ToString()));
                    _ = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                break;
        }
    }

    public async ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
        string? cursor, string? search, bool descending, CancellationToken ct = default)
    {
        var projectionKey = QueryKey(tenantId);
        var cursorScope = AccessControlDirectoryCursor.Scope(projectionKey, "access_team_members", teamId.ToString(), search, descending);
        var after = AccessControlDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var fetchLimit = (take + 1).ToString(CultureInfo.InvariantCulture);
        var direction = descending ? "DESC" : "ASC";
        var comparison = descending ? "<" : ">";
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT team_id, member_id FROM access_team_members WHERE projection_key = @scope" +
            " AND team_id = @parent" +
            (after is null ? "" : $" AND member_id {comparison} @after") +
            $" ORDER BY member_id {direction}" + (search is null ? " LIMIT " + fetchLimit : ""));
        CassieAccessControlSql.Bind(command, ("scope", projectionKey));
        CassieAccessControlSql.Bind(command, ("parent", teamId.ToString()));
        if (after is { } position)
            CassieAccessControlSql.Bind(command, ("after", position.Sort));
        var matches = new List<TeamMemberView>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = new TeamMemberView(CassieAccessControlSql.Id(rows.GetString(0)), CassieAccessControlSql.Id(rows.GetString(1)));
            if (search is not null && !item.MemberId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
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
        return new Page<TeamMemberView>(matches, more ? AccessControlDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }

    public async ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId,
        Uuid memberId, CancellationToken ct = default)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "TeamMemberDirectory", pattern), ct)
            .ConfigureAwait(false);
        var isMember = true;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberRemoved removed when removed.TenantId == tenantId &&
                    removed.TeamId == teamId && removed.MemberId == memberId:
                    isMember = false;
                    break;
                case TeamMemberAssigned assigned when assigned.TenantId == tenantId &&
                    assigned.TeamId == teamId && assigned.MemberId == memberId:
                    isMember = true;
                    break;
            }
        }
        return !isMember;
    }

    public async ValueTask<IReadOnlyList<TeamMemberView>> ListMemberAssignmentsAsync(
        Uuid tenantId, Uuid memberId, CancellationToken ct = default)
    {
        var assignments = new Dictionary<Uuid, TeamMemberView>();
        await using (var read = await OpenReadAsync(ct).ConfigureAwait(false))
        {
            await using var command = read.CreateCommand("SELECT team_id, member_id FROM access_team_members WHERE projection_key = @scope AND member_id = @member ORDER BY team_id");
            CassieAccessControlSql.Bind(command, ("scope", QueryKey(tenantId)), ("member", memberId.ToString()));
            await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rows.ReadAsync(ct).ConfigureAwait(false))
            {
                var item = new TeamMemberView(CassieAccessControlSql.Id(rows.GetString(0)), CassieAccessControlSql.Id(rows.GetString(1)));
                assignments[item.TeamId] = item;
            }
        }

        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "TeamMemberDirectory", pattern), ct).ConfigureAwait(false);
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberAssigned assigned when assigned.TenantId == tenantId &&
                    assigned.MemberId == memberId:
                    assignments[assigned.TeamId] = new TeamMemberView(assigned.TeamId,
                        assigned.MemberId);
                    break;
                case TeamMemberRemoved removed when removed.TenantId == tenantId &&
                    removed.MemberId == memberId:
                    assignments.Remove(removed.TeamId);
                    break;
            }
        }
        return assignments.Values.OrderBy(static item => item.TeamId.ToString(),
            StringComparer.Ordinal).ToArray();
    }

}
