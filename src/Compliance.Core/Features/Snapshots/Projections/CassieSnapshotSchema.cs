namespace Bdgrz.Compliance.Features.Snapshots.Projections;

/// <summary>Application-owned native read tables; Portia owns projection transactions and checkpoints.</summary>
static class CassieSnapshotSchema
{
    public static IReadOnlyList<string> Statements { get; } =
    [
        "CREATE TABLE IF NOT EXISTS scope_snapshots (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS scope_snapshots_scoped ON scope_snapshots (projection_key, scope_id, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS population_snapshots (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS population_snapshots_scoped ON population_snapshots (projection_key, scope_id, sort_key, row_id)",
    ];
}
