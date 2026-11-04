namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

/// <summary>Application-owned native Cassie schema; startup migrations apply it before projection workloads run.</summary>
static class CassieMethodologyTemplateSchema
{
    public const string Catalog = "methodology_template_catalog";
    public const string Versions = "methodology_template_versions";
    public static IReadOnlyList<string> Statements { get; } =
    [
        """
        CREATE TABLE IF NOT EXISTS methodology_template_catalog (
            row_key TEXT PRIMARY KEY,
            projection_key TEXT NOT NULL,
            template_id TEXT NOT NULL,
            first_published_ticks BIGINT NOT NULL,
            separate_draft_line BOOLEAN NOT NULL
        )
        """,
        """
        CREATE INDEX IF NOT EXISTS methodology_template_catalog_order
            ON methodology_template_catalog (projection_key, first_published_ticks, template_id)
        """,
        """
        CREATE TABLE IF NOT EXISTS methodology_template_versions (
            row_key TEXT PRIMARY KEY,
            version_key TEXT NOT NULL,
            projection_key TEXT NOT NULL,
            version_id TEXT NOT NULL,
            template_id TEXT NOT NULL,
            version_number BIGINT NOT NULL,
            published_ticks BIGINT NOT NULL,
            separate_draft_line BOOLEAN NOT NULL
        )
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS methodology_template_versions_number
            ON methodology_template_versions (version_key)
        """,
    ];
}
