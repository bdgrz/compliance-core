using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

/// <summary>
///     Splits a population into ordered storage chunks that each fit one event. A Fitz operation
///     payload has a 16-bit length, so no event may carry a whole large population. Each chunk
///     stays within <see cref="ChunkByteBudget" /> encoded row bytes, which leaves room for the
///     event envelope. The storage manifest digest binds the chunk digests in order.
/// </summary>
public static class PopulationSnapshotStorage
{
    public const int ChunkByteBudget = 32 * 1024;
    public const int MaximumRowsPerChunk = 1024;
    public const int MaximumChunks = 8192;

    static readonly byte[] ManifestDomain =
        Encoding.UTF8.GetBytes("bdgrz.snapshot.population.storage.v1\0");

    /// <summary>Plans the chunks of an already valid, ordered population.</summary>
    public static Result<IReadOnlyList<PopulationStorageChunk>> Plan(string kind,
        IReadOnlyList<PopulationRow> rows)
    {
        var chunks = new List<PopulationStorageChunk>();
        var start = 0;
        var bytes = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            var size = EncodedSize(rows[index]);
            if (size > ChunkByteBudget)
                return Invalid($"Population row {index + 1} exceeds the {ChunkByteBudget}-byte storage chunk budget.");
            if (index > start && (bytes + size > ChunkByteBudget || index - start == MaximumRowsPerChunk))
            {
                if (chunks.Count + 1 == MaximumChunks)
                    return Invalid($"A population snapshot supports at most {MaximumChunks} storage chunks.");
                var closed = Close(kind, rows, chunks.Count, start, index - start, bytes);
                if (!closed.IsSuccess)
                    return Result<IReadOnlyList<PopulationStorageChunk>>.Failure(closed.Error);
                chunks.Add(closed.Value);
                start = index;
                bytes = 0;
            }
            bytes += size;
        }
        if (rows.Count > start)
        {
            var closed = Close(kind, rows, chunks.Count, start, rows.Count - start, bytes);
            if (!closed.IsSuccess)
                return Result<IReadOnlyList<PopulationStorageChunk>>.Failure(closed.Error);
            chunks.Add(closed.Value);
        }
        return Result<IReadOnlyList<PopulationStorageChunk>>.Success(chunks);
    }

    /// <summary>
    ///     The ordered storage manifest digest: SHA-256 over the domain, kind, row and chunk
    ///     counts, then each chunk's row count and digest.
    /// </summary>
    public static string ManifestSha256(string kind, long rowCount,
        IReadOnlyList<PopulationStorageChunk> chunks)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(ManifestDomain);
        hash.AppendData(Encoding.UTF8.GetBytes(kind));
        hash.AppendData([0]);
        var header = new byte[12];
        BinaryPrimitives.WriteInt64BigEndian(header, rowCount);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), chunks.Count);
        hash.AppendData(header);
        var count = new byte[4];
        foreach (var chunk in chunks)
        {
            BinaryPrimitives.WriteInt32BigEndian(count, chunk.Count);
            hash.AppendData(count);
            hash.AppendData(Convert.FromHexString(chunk.Sha256));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static IEnumerable<PopulationRow> Slice(IReadOnlyList<PopulationRow> rows, int start,
        int count)
    {
        for (var index = start; index < start + count; index++)
            yield return rows[index];
    }

    static int EncodedSize(PopulationRow row) =>
        JsonSerializer.SerializeToUtf8Bytes(row, ComplianceCoreJsonContext.Default.PopulationRow)
            .Length + 1;

    static Result<PopulationStorageChunk> Close(string kind, IReadOnlyList<PopulationRow> rows,
        int index, int start, int count, int bytes)
    {
        var digest = PopulationContentIdentity.Compute(kind, Slice(rows, start, count));
        return digest.IsSuccess
            ? Result<PopulationStorageChunk>.Success(
                new PopulationStorageChunk(index, start, count, bytes, digest.Value.Sha256))
            : Result<PopulationStorageChunk>.Failure(digest.Error);
    }

    static Result<IReadOnlyList<PopulationStorageChunk>> Invalid(string message) =>
        Result<IReadOnlyList<PopulationStorageChunk>>.Failure(
            new RequestError(RequestErrorKind.Validation, message));
}
