using System.IO.Compression;
using System.Security.Cryptography;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Services;

/// <summary>Builds exact pinned source bytes into a deterministic ZIP, then verifies the stored result before use.</summary>
public sealed class AuditPackageArchiveBuilder(IArtifactContentStore artifacts)
{
    static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async ValueTask<Result<AuditPackageBuildOutput>> BuildAsync(Uuid tenantId,
        AuditPackageManifestView manifest, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || manifest.TenantId != tenantId || manifest.Entries.Count == 0 ||
            manifest.Entries.Select(entry => entry.RelativePath).Distinct(StringComparer.Ordinal).Count() !=
            manifest.Entries.Count || manifest.Entries.Any(entry => !SafePath(entry.RelativePath) ||
                entry.Source.ByteLength < 0 || !Sha256(entry.Source.ContentSha256) ||
                entry.Source.SourceContent is { } physical && (physical.Bytes != entry.Source.ByteLength ||
                    !Sha256(physical.Sha256) || !StringComparer.OrdinalIgnoreCase.Equals(physical.FileExtension,
                        entry.Source.Extension))))
            return Failure("The archive manifest contains invalid, duplicate or foreign content.", false);
        var temporary = Path.GetTempFileName();
        try
        {
            await using (var output = new FileStream(temporary, FileMode.Truncate, FileAccess.ReadWrite,
                             FileShare.None, 81920, FileOptions.Asynchronous))
            {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var item in manifest.Entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    await using var source = await artifacts.OpenVerifiedAsync(new ArtifactContentReference(tenantId,
                        item.Source.SourceContent?.Sha256 ?? item.Source.ContentSha256, item.Source.ByteLength), ct).ConfigureAwait(false);
                    if (source is null)
                        return Failure("An exact selected source is not available in verified tenant storage.", true);
                    var entry = archive.CreateEntry(item.RelativePath, CompressionLevel.NoCompression);
                    entry.LastWriteTime = ZipEpoch;
                    await using var target = entry.Open();
                    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    long copied = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
                    {
                        copied = checked(copied + read);
                        if (copied > item.Source.ByteLength)
                            return Failure("A selected source exceeds its exact admitted length.", false);
                        digest.AppendData(buffer.AsSpan(0, read));
                        await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    }
                    if (copied != item.Source.ByteLength ||
                        Convert.ToHexStringLower(digest.GetHashAndReset()) !=
                        (item.Source.SourceContent?.Sha256 ?? item.Source.ContentSha256))
                        return Failure("A selected source differs from its exact admitted bytes.", false);
                }
            }
            await using var completed = new FileStream(temporary, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, FileOptions.Asynchronous);
            var stored = await artifacts.StoreIfAbsentAsync(tenantId, completed, ct).ConfigureAwait(false);
            var reference = stored.Content;
            var archiveId = Uuid.CreateVersion5(tenantId, "audit-package-archive:" + reference.Sha256);
            var build = new AuditPackageBuildAttemptView(Uuid.Empty, string.Empty, "built", manifest.Entries.Count,
                manifest.Entries.Count, ZipEpoch, ZipEpoch, null, string.Empty, ZipEpoch, null, null,
                archiveId, reference.Sha256, reference.Length);
            var failure = await AuditPackageArchiveVerifier.FailureCodeAsync(artifacts, tenantId, build,
                manifest, ct).ConfigureAwait(false);
            return failure is null
                ? Result<AuditPackageBuildOutput>.Success(new AuditPackageBuildOutput(
                    manifest.AuditPackageManifestId, manifest.ManifestSha256, manifest.Entries, archiveId,
                    reference.Sha256, reference.Length, null))
                : Failure("The completed archive does not verify against its exact manifest: " + failure,
                    failure == "archive_content_unavailable");
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    static bool SafePath(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 1024 &&
        !path.StartsWith('/') && !path.Contains('\\') &&
        path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
    static bool Sha256(string value) => value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static Result<AuditPackageBuildOutput> Failure(string message, bool transient) =>
        Result<AuditPackageBuildOutput>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
