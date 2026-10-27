namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The complete staged rows of one observation, in authored order, with no count cap.</summary>
public sealed record ApplicationImportRowsManifest(IReadOnlyList<ApplicationImportStagedRow> Rows)
{
    /// <summary>Schema of the rows content referenced by <see cref="ApplicationImportStaged.RowsManifest" />.</summary>
    public const string Schema = "ApplicationImportStaged.RowsManifest.v1";
}
