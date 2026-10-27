using System.IO.Compression;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Rules;

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
