namespace Bdgrz.Compliance.Features.Boundaries.Projections;

/// <summary>Application-owned native read tables; Portia owns projection transactions and checkpoints.</summary>
static class CassieBoundarySchema
{
    public static IReadOnlyList<string> Statements { get; } =
    [
        "CREATE TABLE IF NOT EXISTS boundary_current_identities (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS boundary_current_identities_scoped ON boundary_current_identities (projection_key, scope_id, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS boundary_version_identities (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS boundary_version_identities_scoped ON boundary_version_identities (projection_key, scope_id, sort_key, row_id)",
        "CREATE TABLE IF NOT EXISTS boundary_decision_identities (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, row_id TEXT NOT NULL, scope_id TEXT NOT NULL, sort_key TEXT NOT NULL, payload TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS boundary_decision_identities_scoped ON boundary_decision_identities (projection_key, scope_id, sort_key, row_id)",
    ];
}
