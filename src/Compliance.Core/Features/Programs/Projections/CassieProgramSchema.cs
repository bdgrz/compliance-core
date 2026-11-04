namespace Bdgrz.Compliance.Features.Programs.Projections;

/// <summary>Application-owned native read tables; Portia owns projection transactions and checkpoints.</summary>
static class CassieProgramSchema
{
    public static IReadOnlyList<string> Statements { get; } =
    [
        "CREATE TABLE IF NOT EXISTS program_directory (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS program_directory_ordered ON program_directory (projection_key, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS program_revisions (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS program_revisions_scoped ON program_revisions (projection_key, scope_id, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS client_service_directory (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS client_service_directory_ordered ON client_service_directory (projection_key, sort_key, row_id)",
        "CREATE INDEX IF NOT EXISTS client_service_directory_scoped ON client_service_directory (projection_key, scope_id, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS client_service_revisions (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS client_service_revisions_scoped ON client_service_revisions (projection_key, scope_id, sort_key, row_id)",
    ];
}
