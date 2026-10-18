using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.Examinations;

static class TypeIBaselineContentIdentity
{
    const string Domain = "bdgrz.snapshot.manifest.type_i_baseline.v1\0";

    public static (string Json, string Digest) Manifest(TypeIBaselinePreview preview)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format_version", 1);
            writer.WriteString("kind", "type_i_baseline");
            writer.WriteString("tenant_id", preview.TenantId.ToString());
            writer.WriteString("program_id", preview.ProgramId.ToString());
            writer.WriteString("examination_engagement_id", preview.ExaminationEngagementId.ToString());
            writer.WritePropertyName("sources");
            writer.WriteStartArray();
            foreach (var source in preview.Sources.OrderBy(static source => source.Kind,
                         StringComparer.Ordinal).ThenBy(static source => source.RecordId.ToString(),
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("kind", source.Kind);
                writer.WriteString("record_id", source.RecordId.ToString());
                writer.WriteString("version_id", source.VersionId.ToString());
                writer.WriteNumber("revision", source.Revision);
                writer.WriteString("content_sha256", source.ContentSha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("scope_differences");
            writer.WriteStartArray();
            foreach (var difference in preview.ScopeDifferences.Select(static value =>
                         value.Normalize(NormalizationForm.FormC)).OrderBy(static value => value,
                         StringComparer.Ordinal))
                writer.WriteStringValue(difference);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = stream.ToArray();
        var domain = Encoding.UTF8.GetBytes(Domain);
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return (Encoding.UTF8.GetString(bytes),
            Convert.ToHexStringLower(SHA256.HashData(material)));
    }
}
