using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
/// Explicit capture scope for a source-freeze read. Existing digest algorithms remain unchanged; only the exact
/// material they hash is observed. Full population rows use an independent physical envelope because their logical
/// digest is a Merkle identity. Candidate files are private temporary data and are removed on every exit path.
/// </summary>
static class SnapshotSourceCapture
{
    static readonly AsyncLocal<Scope?> Current = new();

    public static Scope Begin(Uuid tenant) => new(tenant);

    public static byte[] HashData(ReadOnlySpan<byte> material)
    {
        var digest = SHA256.HashData(material);
        Current.Value?.Flat(Convert.ToHexStringLower(digest), material);
        return digest;
    }

    public static void Population(string kind, IReadOnlyList<PopulationRow> rows, string logicalSha256)
    {
        if (Current.Value is { } scope)
            scope.Population(kind, rows, logicalSha256);
    }

    public static void Risk(string logicalSha256, ReadOnlySpan<byte> source,
        IReadOnlyList<SnapshotSourceContentBlob> blobs)
    {
        if (blobs.Count > 0 && Current.Value is { } scope)
            scope.Risk(logicalSha256, source, blobs);
    }

    internal sealed class Scope : IDisposable
    {
        readonly Scope? _parent;
        readonly string _directory = Directory.CreateTempSubdirectory("bdgrz-source-capture-").FullName;
        readonly Dictionary<string, Material> _materials = new(StringComparer.Ordinal);
        readonly object _gate = new();
        bool _disposed;

        public Scope(Uuid tenant)
        {
            Tenant = tenant;
            _parent = Current.Value;
            Current.Value = this;
        }

        public Uuid Tenant { get; }
        public bool TryGet(string logicalSha256, out Material? material)
        {
            lock (_gate)
                return _materials.TryGetValue(logicalSha256, out material);
        }

        public void Flat(string logicalSha256, ReadOnlySpan<byte> bytes)
        {
            lock (_gate)
            {
                if (_materials.ContainsKey(logicalSha256))
                    return;
                var path = Path.Combine(_directory, logicalSha256 + ".source");
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    output.Write(bytes);
                var separator = bytes.IndexOf((byte)0);
                var domain = separator is > 0 and < 256 ? Encoding.UTF8.GetString(bytes[..separator]) : null;
                var prefixed = domain?.StartsWith("bdgrz.", StringComparison.Ordinal) == true;
                var json = !prefixed && IsJson(bytes);
                var descriptor = new SnapshotSourceBlob(logicalSha256, bytes.Length,
                    prefixed ? "application/vnd.bdgrz.canonical-snapshot-source.v1" :
                        json ? "application/json" : "application/octet-stream",
                    prefixed ? "snapshot" : json ? "json" : "bin",
                    prefixed ? $"Canonical snapshot source proof bytes ({domain}, NUL delimiter, exact payload)." :
                        "Exact canonical source bytes.");
                _materials.Add(logicalSha256, new Material(path, descriptor));
            }
        }

        public void Population(string kind, IReadOnlyList<PopulationRow> rows, string logicalSha256)
        {
            var frozenRows = rows.ToArray();
            var identity = PopulationContentIdentity.Compute(kind, frozenRows);
            if (!identity.IsSuccess || identity.Value.Sha256 != logicalSha256)
                throw new SnapshotContentException("A population export must retain its verified complete logical identity.");
            lock (_gate)
            {
                if (_materials.TryGetValue(logicalSha256, out var existing))
                {
                    if (existing.Blob.MediaType == "application/vnd.bdgrz.population-snapshot-source.v1+json")
                        return;
                    File.Delete(existing.Path);
                    _materials.Remove(logicalSha256);
                }
                var path = Path.Combine(_directory, logicalSha256 + ".source");
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new Utf8JsonWriter(output, SnapshotContentIdentity.CanonicalWriterOptions))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("format_version", 1);
                    writer.WriteString("kind", kind);
                    writer.WriteString("logical_content_sha256", logicalSha256);
                    writer.WriteNumber("row_count", identity.Value.RowCount);
                    writer.WriteNumber("logical_chunk_count", identity.Value.ChunkCount);
                    writer.WriteStartArray("rows");
                    foreach (var row in frozenRows)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("key", SnapshotContentIdentity.NormalizeText(row.Key));
                        writer.WritePropertyName("content");
                        SnapshotContentIdentity.WriteCanonical(writer, row.Content);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                using var input = File.OpenRead(path);
                var physical = Convert.ToHexStringLower(SHA256.HashData(input));
                var descriptor = new SnapshotSourceBlob(physical, input.Length,
                    "application/vnd.bdgrz.population-snapshot-source.v1+json", "json",
                    "Complete canonical population rows and their verified logical Merkle identity.");
                _materials.Add(logicalSha256, new Material(path, descriptor));
            }
        }

        public void Risk(string logicalSha256, ReadOnlySpan<byte> source,
            IReadOnlyList<SnapshotSourceContentBlob> blobs)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(source)) != logicalSha256)
                throw new ImmutableContentIntegrityException("A risk source envelope differs from its exact logical preimage.");
            var unique = new Dictionary<Uuid, SnapshotSourceContentBlob>();
            foreach (var blob in blobs)
            {
                if (blob.Reference.ContentId == Uuid.Empty || blob.Reference.Bytes != blob.Bytes.LongLength ||
                    Convert.ToHexStringLower(SHA256.HashData(blob.Bytes)) != blob.Reference.Sha256 ||
                    unique.TryGetValue(blob.Reference.ContentId, out var prior) && prior.Reference != blob.Reference)
                    throw new ImmutableContentIntegrityException("A risk source blob differs from its exact immutable reference.");
                unique.TryAdd(blob.Reference.ContentId, blob);
            }
            lock (_gate)
            {
                if (_materials.TryGetValue(logicalSha256, out var existing))
                {
                    File.Delete(existing.Path);
                    _materials.Remove(logicalSha256);
                }
                var path = Path.Combine(_directory, logicalSha256 + ".source");
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new Utf8JsonWriter(output, SnapshotContentIdentity.CanonicalWriterOptions))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("format_version", 1);
                    writer.WriteString("logical_content_sha256", logicalSha256);
                    writer.WriteBase64String("source_base64", source);
                    writer.WriteStartArray("content_blobs");
                    foreach (var blob in unique.Values.OrderBy(item => item.Reference.ContentId.ToString(), StringComparer.Ordinal))
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("reference");
                        var reference = JsonSerializer.SerializeToElement(blob.Reference,
                            ComplianceCoreJsonContext.Default.ImmutableContentRef);
                        SnapshotContentIdentity.WriteCanonical(writer, reference);
                        writer.WriteBase64String("base64", blob.Bytes);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                using var input = File.OpenRead(path);
                var physical = Convert.ToHexStringLower(SHA256.HashData(input));
                var descriptor = new SnapshotSourceBlob(physical, input.Length,
                    "application/vnd.bdgrz.risk-snapshot-source.v1+json", "json",
                    "Exact canonical risk source proof and its digest-verified immutable business content bytes.");
                _materials.Add(logicalSha256, new Material(path, descriptor));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (ReferenceEquals(Current.Value, this))
                    Current.Value = _parent;
                Directory.Delete(_directory, recursive: true);
            }
        }

        static bool IsJson(ReadOnlySpan<byte> bytes)
        {
            try
            {
                using var value = JsonDocument.Parse(bytes.ToArray());
                return value.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    internal sealed record Material(string Path, SnapshotSourceBlob Blob);
}
