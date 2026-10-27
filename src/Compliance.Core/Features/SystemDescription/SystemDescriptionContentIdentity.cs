using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.SystemDescription;

static class SystemDescriptionContentIdentity
{
    const string Domain = "bdgrz.system_description.version.v1\0";

    public static string Digest(SystemDescriptionScope scope, SystemDescriptionDraftContent content)
    {
        var model = new SystemDescriptionVersionContent(scope, content);
        var element = JsonSerializer.SerializeToElement(model,
            ComplianceCoreJsonContext.Default.SystemDescriptionVersionContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var canonical = stream.ToArray();
        var domain = Encoding.UTF8.GetBytes(Domain);
        var material = new byte[domain.Length + canonical.Length];
        domain.CopyTo(material, 0);
        canonical.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }
}
