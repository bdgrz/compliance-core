namespace Bdgrz.Compliance.Features.Workforce.Projections;

/// <summary>Application tables initialized before Workforce projector workloads start.</summary>
static class CassieWorkforceSchema
{
    public static IReadOnlyList<string> Statements { get; } =
    [
        "CREATE TABLE IF NOT EXISTS workforce_people (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, sort_key TEXT NOT NULL, correlated_user_id TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_people_names ON workforce_people USING btree (projection_key, sort_key, model_id)",
        "CREATE INDEX IF NOT EXISTS workforce_people_members ON workforce_people USING btree (projection_key, correlated_user_id, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_work_relationships (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, sort_key TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_relationship_workers ON workforce_work_relationships USING btree (projection_key, sort_key, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_service_identities (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, sort_key TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_identity_names ON workforce_service_identities USING btree (projection_key, sort_key, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_source_observations (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, sort_key TEXT NOT NULL, target_kind TEXT NOT NULL, target_id TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_sources_observed ON workforce_source_observations USING btree (projection_key, sort_key, model_id)",
        "CREATE INDEX IF NOT EXISTS workforce_sources_target ON workforce_source_observations USING btree (projection_key, target_kind, target_id, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_relationship_snapshots (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, person_id TEXT NOT NULL, worker_type TEXT NOT NULL, lifecycle_status TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_snapshots_person ON workforce_relationship_snapshots USING btree (projection_key, person_id, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_observations (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL, sort_key TEXT NOT NULL, kind TEXT NOT NULL)",
        "CREATE INDEX IF NOT EXISTS workforce_observations_observed ON workforce_observations USING btree (projection_key, sort_key, model_id)",
        "CREATE INDEX IF NOT EXISTS workforce_observations_kind ON workforce_observations USING btree (projection_key, kind, sort_key, model_id)",
        "CREATE TABLE IF NOT EXISTS workforce_observation_resolutions (row_key TEXT PRIMARY KEY, projection_key TEXT NOT NULL, model_id TEXT NOT NULL, row_json TEXT NOT NULL)",
    ];
}
