namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Application SQL schema initialized before the seven access-control projector workloads.</summary>
static class CassieAccessControlSchema
{
    public static IReadOnlyList<string> Statements { get; } =
    [
        "CREATE TABLE IF NOT EXISTS access_teams (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, team_id TEXT NOT NULL, name TEXT NOT NULL, sort_name TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_team_members (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, team_id TEXT NOT NULL, member_id TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_roles (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, role_id TEXT NOT NULL, name TEXT NOT NULL, sort_name TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_role_permissions (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, role_id TEXT NOT NULL, permission TEXT NOT NULL, assignment_id TEXT NOT NULL, revision TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_role_teams (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, role_id TEXT NOT NULL, team_id TEXT NOT NULL, assignment_id TEXT NOT NULL, revision TEXT NOT NULL, scope_payload TEXT NOT NULL, sort_key TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_team_roles (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, team_id TEXT NOT NULL, role_id TEXT NOT NULL, assignment_id TEXT NOT NULL, revision TEXT NOT NULL, scope_payload TEXT NOT NULL, sort_key TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_permission_states (projection_key TEXT PRIMARY KEY, payload TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS access_permission_grants (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, member_id TEXT NOT NULL, permission TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS access_team_members_by_team ON access_team_members USING btree (projection_key, team_id, member_id)",
        "CREATE INDEX IF NOT EXISTS access_role_permissions_by_role ON access_role_permissions USING btree (projection_key, role_id, permission)",
        "CREATE INDEX IF NOT EXISTS access_role_teams_by_role ON access_role_teams USING btree (projection_key, role_id, sort_key)",
        "CREATE INDEX IF NOT EXISTS access_team_roles_by_team ON access_team_roles USING btree (projection_key, team_id, sort_key)",
        "CREATE INDEX IF NOT EXISTS access_permission_grants_by_member ON access_permission_grants USING btree (projection_key, member_id, permission)",
        "CREATE INDEX IF NOT EXISTS access_teams_by_name ON access_teams USING btree (projection_key, sort_name, team_id)",
        "CREATE INDEX IF NOT EXISTS access_roles_by_name ON access_roles USING btree (projection_key, sort_name, role_id)",
        "CREATE INDEX IF NOT EXISTS access_team_members_by_member ON access_team_members USING btree (projection_key, member_id, team_id)",
    ];
}
