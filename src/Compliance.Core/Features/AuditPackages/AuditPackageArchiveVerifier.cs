using System.IO.Compression;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Artifacts;

namespace Bdgrz.Compliance.Features.AuditPackages;

static class AuditPackageArchiveVerifier
{
    public static async ValueTask<string?> FailureCodeAsync(IArtifactContentStore store,
        Cntryl.Portia.Uuid tenantId, AuditPackageBuildAttemptView build,
        AuditPackageManifestView manifest, CancellationToken ct)
    {
        if (build.ArchiveSha256 is not { } sha256 || build.ArchiveBytes is not > 0)
            return "archive_content_unavailable";

        try
        {
            await using var content = await store.OpenVerifiedAsync(
                new ArtifactContentReference(tenantId, sha256, build.ArchiveBytes.Value), ct)
                .ConfigureAwait(false);
            if (content is null)
                return "archive_content_unavailable";

            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count != manifest.Entries.Count ||
                archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal)
                    .Count() != archive.Entries.Count)
                return "archive_content_mismatch";

            var byPath = archive.Entries.ToDictionary(entry => entry.FullName,
                StringComparer.Ordinal);
            foreach (var expected in manifest.Entries)
            {
                if (!byPath.TryGetValue(expected.RelativePath, out var entry) ||
                    entry.Length != expected.Source.ByteLength)
                    return "archive_content_mismatch";
                await using var source = entry.Open();
                var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(source, ct)
                    .ConfigureAwait(false));
                if (!StringComparer.Ordinal.Equals(digest,
                        expected.Source.SourceContent?.Sha256 ?? expected.Source.ContentSha256))
                    return "archive_content_mismatch";
            }

            return null;
        }
        catch (InvalidDataException)
        {
            return "archive_content_mismatch";
        }
    }
}
