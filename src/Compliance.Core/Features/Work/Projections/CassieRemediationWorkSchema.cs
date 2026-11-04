namespace Bdgrz.Compliance.Features.Work.Projections;

/// <summary>Application tables for the two remediation work repositories; Portia owns checkpoint metadata.</summary>
public static class CassieRemediationWorkSchema
{
    public static IReadOnlyList<string> Statements { get; } = Array.AsReadOnly(new[]
    {
        """
        CREATE TABLE IF NOT EXISTS work_corrective_actions (
            projection_key TEXT NOT NULL,
            tenant_id TEXT NOT NULL,
            program_id TEXT NOT NULL,
            finding_id TEXT NOT NULL,
            work_item_id TEXT NOT NULL,
            payload JSONB NOT NULL,
            PRIMARY KEY (projection_key, program_id, work_item_id)
        )
        """,
        """
        CREATE INDEX IF NOT EXISTS work_corrective_actions_by_finding
            ON work_corrective_actions (projection_key, program_id, finding_id)
        """,
        """
        CREATE TABLE IF NOT EXISTS work_corrective_action_revisions (
            projection_key TEXT PRIMARY KEY,
            tenant_id TEXT NOT NULL,
            revision BIGINT NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS work_finding_closures (
            projection_key TEXT NOT NULL,
            tenant_id TEXT NOT NULL,
            program_id TEXT NOT NULL,
            finding_id TEXT NOT NULL,
            payload JSONB NOT NULL,
            PRIMARY KEY (projection_key, program_id, finding_id)
        )
        """,
    });
}
