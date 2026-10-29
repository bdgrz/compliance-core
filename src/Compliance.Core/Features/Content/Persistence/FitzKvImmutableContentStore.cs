using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Content.Persistence;

/// <summary>
/// Stores immutable content on <c>kv://bdgrz/immutable-content/{realm}-{shard}</c> (Fitz routes have exactly three
/// segments, so the fixed two-character shard is joined to the realm with a hyphen, which is unambiguous). A write is one ReadWrite
/// transaction: Fitz locks the route at BEGIN, so concurrent writers fail with an isolation conflict, and the
/// chunks and the manifest (the publish point, inserted last) commit together or not at all.
/// </summary>
sealed class FitzKvImmutableContentStore(IKvClient client) : IImmutableContentStore
{
    /// <summary>Largest chunk payload; with route and key it stays inside the 65535 frame budget.</summary>
    internal const int ChunkBytes = 48 * 1024;

    /// <summary>Largest accepted content, bounding the single write transaction.</summary>
    internal const long MaximumContentBytes = 16L * 1024 * 1024;

    /// <summary>Attempts per write; an isolation conflict between unrelated writers of one shard is retried from BEGIN.</summary>
    internal const int WriteAttempts = 4;

    public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
        string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await WriteOnceAsync(realm, contentId, schema, mediaType, bytes, ct).ConfigureAwait(false);
            }
            catch (KvException exception) when (attempt < WriteAttempts && IsIsolationConflict(exception))
            {
                // The route lock was lost to another writer of this shard; the transaction has no partial effect.
            }
        }
    }

    /// <summary>True when the broker reports that the transaction lost an isolation conflict and must restart.</summary>
    internal static bool IsIsolationConflict(KvException exception) =>
        exception.DomainCode == FitzErrorCodes.KvIsolationConflict ||
        string.Equals(exception.Code, nameof(FitzErrorCodes.KvIsolationConflict), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(exception.Code, "KV_ISOLATION_CONFLICT", StringComparison.OrdinalIgnoreCase);

    async Task<ImmutableContentRef> WriteOnceAsync(string realm, Uuid contentId, string schema,
        string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        ValidateRealm(realm);
        ValidateContentId(contentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        if (bytes.Length > MaximumContentBytes)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Immutable content exceeds the maximum size.");

        var reference = new ImmutableContentRef(contentId, ContentDigest.Sha256(bytes.Span), bytes.Length,
            mediaType, schema);
        var chunkCount = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
        var manifest = new ImmutableContentManifest(contentId, reference.Sha256, reference.Bytes, mediaType, schema,
            chunkCount, ChunkBytes);
        await using var tx = await client.BeginAsync(Route(realm, contentId), KvMode.ReadWrite, ct)
            .ConfigureAwait(false);
        var existing = await tx.GetAsync(ManifestKey(contentId), ct).ConfigureAwait(false);
        if (existing.Found)
        {
            var stored = Parse(existing.Value!.Value.Span);
            if (stored.Sha256 == manifest.Sha256 && stored.Bytes == manifest.Bytes &&
                stored.MediaType == manifest.MediaType && stored.Schema == manifest.Schema)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return reference;
            }
            throw new ImmutableContentConflictException(
                $"Content {contentId} already exists with different bytes, media type or schema.");
        }

        for (var index = 0; index < chunkCount; index++)
        {
            var chunk = bytes.Slice(index * ChunkBytes, Math.Min(ChunkBytes, bytes.Length - (index * ChunkBytes)));
            await tx.InsertAsync(ChunkKey(contentId, index), chunk, ct).ConfigureAwait(false);
        }
        await tx.InsertAsync(ManifestKey(contentId),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ComplianceCoreJsonContext.Default.ImmutableContentManifest),
            ct).ConfigureAwait(false);
        await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
        return reference;
    }

    public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
        CancellationToken ct = default)
    {
        ValidateRealm(realm);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateContentId(reference.ContentId);
        if (reference.Bytes is < 0 or > MaximumContentBytes)
            throw new ImmutableContentIntegrityException("The content reference length is out of range.");

        await using var tx = await client.BeginAsync(Route(realm, reference.ContentId), KvMode.ReadOnly, ct)
            .ConfigureAwait(false);
        var found = await tx.GetAsync(ManifestKey(reference.ContentId), ct).ConfigureAwait(false);
        if (!found.Found)
            throw new ImmutableContentUnavailableException($"Content {reference.ContentId} has no manifest.");
        var manifest = Parse(found.Value!.Value.Span);
        var expectedChunks = (int)((reference.Bytes + ChunkBytes - 1) / ChunkBytes);
        if (manifest.ContentId != reference.ContentId || manifest.Bytes != reference.Bytes ||
            manifest.Schema != reference.Schema || manifest.MediaType != reference.MediaType ||
            !string.Equals(manifest.Sha256, reference.Sha256, StringComparison.OrdinalIgnoreCase) ||
            manifest.ChunkBytes != ChunkBytes || manifest.ChunkCount != expectedChunks)
            throw new ImmutableContentIntegrityException(
                $"Content {reference.ContentId} does not match the reference length, schema or digest.");

        var buffer = new byte[reference.Bytes];
        var written = 0;
        for (var index = 0; index < expectedChunks; index++)
        {
            var chunk = await tx.GetAsync(ChunkKey(reference.ContentId, index), ct).ConfigureAwait(false);
            var expected = Math.Min(ChunkBytes, buffer.Length - written);
            if (!chunk.Found)
                throw new ImmutableContentUnavailableException($"Content {reference.ContentId} chunk {index} is missing.");
            if (chunk.Value!.Value.Length != expected)
                throw new ImmutableContentIntegrityException(
                    $"Content {reference.ContentId} chunk {index} has the wrong length.");
            chunk.Value.Value.Span.CopyTo(buffer.AsSpan(written));
            written += expected;
        }
        if (!string.Equals(ContentDigest.Sha256(buffer), reference.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ImmutableContentIntegrityException($"Content {reference.ContentId} failed digest verification.");
        return buffer;
    }

    static ImmutableContentManifest Parse(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.ImmutableContentManifest)
                ?? throw new ImmutableContentIntegrityException("The content manifest is null.");
        }
        catch (JsonException exception)
        {
            throw new ImmutableContentIntegrityException($"The content manifest is corrupt: {exception.Message}");
        }
    }

    static void ValidateRealm(string realm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        foreach (var c in realm)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
                throw new ArgumentException("A realm may contain only ASCII letters, digits, '-', '_' and '.'.",
                    nameof(realm));
    }

    static void ValidateContentId(Uuid contentId)
    {
        if (contentId == Uuid.Empty)
            throw new ArgumentException("A content id is required.", nameof(contentId));
    }

    static string Hex(Uuid contentId) => contentId.ToString().Replace("-", "", StringComparison.Ordinal)
        .ToLowerInvariant();

    static string Route(string realm, Uuid contentId) =>
        $"kv://bdgrz/immutable-content/{realm}-{Hex(contentId)[..2]}";

    static byte[] ManifestKey(Uuid contentId) => Encoding.UTF8.GetBytes($"m/{Hex(contentId)}");

    static byte[] ChunkKey(Uuid contentId, int index) => Encoding.UTF8.GetBytes($"c/{Hex(contentId)}/{index:D6}");
}
