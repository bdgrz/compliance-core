using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Services;

/// <summary>Renders the exact approved narrative. Unsupported PDF glyphs fail explicitly; no legal text is replaced or dropped.</summary>
public static class SystemDescriptionDocumentRenderer
{
    public static Result<SystemDescriptionRenderedDocument> Render(SystemDescriptionExportIntent intent, SystemDescriptionVersionView version,
        ReadOnlyMemory<byte> exactSource)
    {
        var paragraphs = Paragraphs(intent, version);
        try
        {
            foreach (var text in paragraphs)
                XmlConvert.VerifyXmlChars(text);
        }
        catch (XmlException)
        {
            return Failure("The export contains characters that the document format cannot represent.");
        }
        return intent.Format switch
        {
            "docx" => Result<SystemDescriptionRenderedDocument>.Success(new(Docx(intent, paragraphs, exactSource),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx")),
            "pdf" => Pdf(paragraphs, exactSource),
            _ => Failure("The requested export format is unsupported."),
        };
    }
    static List<string> Paragraphs(SystemDescriptionExportIntent intent, SystemDescriptionVersionView version)
    {
        var values = new List<string>
        {
            version.Content.Title,
            $"Tenant: {intent.TenantId}", $"Program: {intent.ProgramId}", $"Description: {intent.DescriptionId}",
            $"Version: {intent.Version} ({intent.VersionId})", $"Approved content SHA-256: {intent.ContentSha256}",
            $"Exact source bytes SHA-256: {intent.SourceContent.Sha256}",
            $"Examination: {version.Scope.ExaminationId}; stage: {version.Scope.Stage}",
            $"Type I baseline: {version.Scope.TypeIBaselineId}; Type II period: {version.Scope.TypeIIPeriodId}; close: {version.Scope.CloseSnapshotId}",
            $"Generated: {intent.Operation.AcceptedAt.ToString("O",CultureInfo.InvariantCulture)}; by: {intent.Operation.Actor.Display}",
        };
        foreach (var section in version.Content.Sections)
        {
            values.Add(section.Title);
            values.Add($"Section: {section.SectionId}; status: {section.Status}");
            values.Add(section.Content);
            if (!string.IsNullOrEmpty(section.NotApplicableRationale))
                values.Add(section.NotApplicableRationale);
        }
        values.Add("Governed source versions");
        foreach (var source in version.Content.Sources)
            values.Add($"{source.Kind}: {source.RecordId}; version: {source.VersionId}; revision: {source.Revision}; SHA-256: {source.ContentSha256}; status: {source.Status}");
        values.Add("Period changes");
        foreach (var change in version.Content.PeriodChanges)
            values.Add($"{change.ChangeId}; material: {change.IsMaterial}; assessment SHA-256: {change.AssessmentSha256}");
        values.Add("Significant changes");
        values.AddRange(version.Content.SignificantChangeIds.Select(item => item.ToString()));
        return values;
    }
    static byte[] Docx(SystemDescriptionExportIntent intent, IReadOnlyList<string> paragraphs, ReadOnlyMemory<byte> source)
    {
        using var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(zip, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                Empty(writer, "Default", ("Extension", "rels"), ("ContentType", "application/vnd.openxmlformats-package.relationships+xml"));
                Empty(writer, "Default", ("Extension", "xml"), ("ContentType", "application/xml"));
                Empty(writer, "Override", ("PartName", "/word/document.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"));
                writer.WriteEndElement();
            });
            WriteXml(zip, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                Empty(writer, "Relationship", ("Id", "rId1"), ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), ("Target", "word/document.xml"));
                writer.WriteEndElement();
            });
            WriteXml(zip, "word/_rels/document.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                Empty(writer, "Relationship", ("Id", "rIdSource"), ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml"), ("Target", "../customXml/item1.xml"));
                writer.WriteEndElement();
            });
            WriteXml(zip, "word/document.xml", writer =>
            {
                const string word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                writer.WriteStartElement("w", "document", word);
                writer.WriteStartElement("w", "body", word);
                foreach (var paragraph in paragraphs)
                    foreach (var line in Lines(paragraph))
                    {
                        writer.WriteStartElement("w", "p", word);
                        writer.WriteStartElement("w", "r", word);
                        writer.WriteStartElement("w", "t", word);
                        writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                        writer.WriteString(line);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
            WriteXml(zip, "customXml/item1.xml", writer =>
            {
                writer.WriteStartElement("approved-source", "urn:bdgrz:system-description:approved-source:v1");
                writer.WriteAttributeString("tenant_id", intent.TenantId.ToString());
                writer.WriteAttributeString("program_id", intent.ProgramId.ToString());
                writer.WriteAttributeString("version_id", intent.VersionId.ToString());
                writer.WriteAttributeString("sha256", intent.SourceContent.Sha256);
                writer.WriteBase64(source.ToArray(), 0, source.Length);
                writer.WriteEndElement();
            });
        }
        return result.ToArray();
    }
    static void WriteXml(ZipArchive zip, string path, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        write(writer);
    }
    static void Empty(XmlWriter writer, string name, params (string Name, string Value)[] attributes)
    {
        writer.WriteStartElement(name, name == "Relationship" ? "http://schemas.openxmlformats.org/package/2006/relationships" :
            "http://schemas.openxmlformats.org/package/2006/content-types");
        foreach (var (key, value) in attributes)
            writer.WriteAttributeString(key, value);
        writer.WriteEndElement();
    }
    static Result<SystemDescriptionRenderedDocument> Pdf(IReadOnlyList<string> paragraphs, ReadOnlyMemory<byte> source)
    {
        var lines = new List<string>();
        foreach (var paragraph in paragraphs)
            foreach (var line in Lines(paragraph))
            {
                if (line.Any(character => !PdfByte(character, out _)))
                    return Failure("PDF export cannot represent one or more characters. Use DOCX.");
                if (line.Length == 0)
                    lines.Add(string.Empty);
                for (var start = 0; start < line.Length; start += 90)
                    lines.Add(line.Substring(start, Math.Min(90, line.Length - start)));
            }
        var pages = Math.Max(1, (lines.Count + 51) / 52);
        var objects = new List<byte[]> { Array.Empty<byte>(), Array.Empty<byte>(), Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>") };
        var children = new List<int>();
        for (var page = 0; page < pages; page++)
        {
            var pageId = objects.Count + 1;
            var contentId = pageId + 1;
            children.Add(pageId);
            objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>"));
            var text = new StringBuilder("BT /F1 9 Tf 40 800 Td 13 TL\n");
            foreach (var line in lines.Skip(page * 52).Take(52))
                text.Append('(').Append(PdfString(line)).Append(") Tj T*\n");
            text.Append("ET\n");
            objects.Add(Stream(Encoding.ASCII.GetBytes(text.ToString()), ""));
        }
        var embedded = objects.Count + 1;
        objects.Add(Stream(source.ToArray(), "/Type /EmbeddedFile /Subtype /application#2Fjson "));
        var file = objects.Count + 1;
        objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Filespec /F (system-description-source.json) /UF (system-description-source.json) /AFRelationship /Source /EF << /F {embedded} 0 R >> >>"));
        objects[0] = Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(system-description-source.json) {file} 0 R] >> >> /AF [{file} 0 R] >>");
        objects[1] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pages} /Kids [{string.Join(' ', children.Select(id => $"{id} 0 R"))}] >>");
        using var result = new MemoryStream();
        result.Write("%PDF-1.7\n"u8);
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(result.Position);
            result.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            result.Write(objects[index]);
            result.Write("\nendobj\n"u8);
        }
        var xref = result.Position;
        result.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1))
            result.Write(Encoding.ASCII.GetBytes(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n"));
        result.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return Result<SystemDescriptionRenderedDocument>.Success(new(result.ToArray(), "application/pdf", "pdf"));
    }
    static byte[] Stream(byte[] bytes, string attributes)
    {
        using var value = new MemoryStream();
        value.Write(Encoding.ASCII.GetBytes($"<< {attributes}/Length {bytes.Length} >>\nstream\n"));
        value.Write(bytes);
        value.Write("\nendstream"u8);
        return value.ToArray();
    }
    static string[] Lines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    static string PdfString(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value)
        {
            PdfByte(character, out var b);
            if (b is (byte)'(' or (byte)')' or (byte)'\\')
                result.Append('\\').Append((char)b);
            else if (b is < 32 or > 126)
                result.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else
                result.Append((char)b);
        }
        return result.ToString();
    }
    static bool PdfByte(char value, out byte result)
    {
        if (value is >= ' ' and <= '~' or >= '\u00a0' and <= '\u00ff')
        { result = (byte)value; return true; }
        var index = "€\0‚ƒ„…†‡ˆ‰Š‹Œ\0Ž\0\0‘’“”•–—˜™š›œ\0žŸ".IndexOf(value);
        if (index >= 0 && value != '\0')
        { result = (byte)(128 + index); return true; }
        result = 0;
        return false;
    }
    static Result<SystemDescriptionRenderedDocument> Failure(string message) => Result<SystemDescriptionRenderedDocument>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
