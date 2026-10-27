using Bdgrz.Compliance.Features.AuditPopulations.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Builds a canonical human- and machine-readable document from one immutable population version.</summary>
static class AuditPopulationExportDocument
{
    static readonly string[] BaseColumns =
    [
        "population_id", "population_version", "population_content_sha256", "close_snapshot_id",
        "row_id", "classification",
    ];

    public static byte[] Build(AuditPopulationView population, AuditPopulationVersionView version,
        string format)
    {
        var definition = population.DefinitionVersions.FirstOrDefault(item =>
            item.Version == version.DefinitionVersion)?.Content ??
            throw new InvalidOperationException("The frozen population version has no matching definition.");
        var fields = definition.AuthorizedFields.Order(StringComparer.Ordinal).ToArray();
        var rows = version.Rows.OrderBy(item => item.RowId.ToString(), StringComparer.Ordinal).ToArray();
        return format switch
        {
            "csv" => BuildCsv(population, version, fields, rows),
            "json" => BuildJson(population, version, fields, rows),
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Only CSV and JSON exports are supported."),
        };
    }

    static byte[] BuildCsv(AuditPopulationView population, AuditPopulationVersionView version,
        IReadOnlyList<string> fields, IReadOnlyList<Bdgrz.Compliance.Features.AuditPopulations.Models.AuditPopulationRowView> rows)
    {
        var columns = BaseColumns.Concat(fields).ToArray();
        var output = new StringBuilder();
        AppendCsvRow(output, columns);
        foreach (var row in rows)
        {
            var values = new List<string>
            {
                population.AuditPopulationId.ToString(),
                version.Version.ToString(CultureInfo.InvariantCulture),
                version.ContentSha256,
                version.CloseSnapshotId.ToString(),
                row.RowId.ToString(),
                row.Classification,
            };
            values.AddRange(fields.Select(field => row.AuthorizedFields.TryGetValue(field, out var value)
                ? value : string.Empty));
            AppendCsvRow(output, values);
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    static byte[] BuildJson(AuditPopulationView population, AuditPopulationVersionView version,
        IReadOnlyList<string> fields, IReadOnlyList<Bdgrz.Compliance.Features.AuditPopulations.Models.AuditPopulationRowView> rows)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("population_id", population.AuditPopulationId.ToString());
            writer.WriteNumber("population_version", version.Version);
            writer.WriteString("population_content_sha256", version.ContentSha256);
            writer.WriteString("close_snapshot_id", version.CloseSnapshotId.ToString());
            writer.WriteStartArray("rows");
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                writer.WriteString("row_id", row.RowId.ToString());
                writer.WriteString("classification", row.Classification);
                writer.WriteStartObject("fields");
                foreach (var field in fields)
                    if (row.AuthorizedFields.TryGetValue(field, out var value))
                        writer.WriteString(field, value);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    static void AppendCsvRow(StringBuilder output, IEnumerable<string> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                output.Append(',');
            first = false;
            output.Append(EscapeCsv(value));
        }
        output.Append('\n');
    }

    static string EscapeCsv(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'
            : value;
    }
}
