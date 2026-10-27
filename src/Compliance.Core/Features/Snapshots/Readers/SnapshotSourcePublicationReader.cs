using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Readers;

/// <summary>Reads one complete publication proof; a physical pointer never replaces its logical source identity.</summary>
static class SnapshotSourcePublicationReader
{
    public static async Task<SnapshotSourcePublicationManifest> ReadAsync(IImmutableContentStore content,
        Uuid tenant, Uuid program, Uuid examination, ImmutableContentRef reference, CancellationToken ct)
    {
        if (!ValidReference(reference))
            throw new ImmutableContentIntegrityException("The source publication has another declared schema.");
        var manifest = await content.ReadJsonAsync<SnapshotSourcePublicationManifest>(tenant.ToString(), reference, ct)
            .ConfigureAwait(false);
        if (manifest.TenantId != tenant || manifest.ProgramId != program || manifest.ExaminationId != examination ||
            manifest.Sources is null || manifest.Sources.Select(source => (source.SourceType, source.RecordId,
                source.VersionId, source.Version, source.LogicalSha256)).Distinct().Count() != manifest.Sources.Count ||
            manifest.Sources.Any(source => source.Content is null || source.Content.Bytes <= 0 ||
                !Sha256(source.Content.Sha256) || string.IsNullOrWhiteSpace(source.Content.MediaType) ||
                string.IsNullOrWhiteSpace(source.Content.FileExtension)))
            throw new ImmutableContentIntegrityException("The complete publication differs from its exact source scope or byte proof.");
        return manifest;
    }

    public static bool ValidReference(ImmutableContentRef? reference) => reference is null ||
        reference.ContentId != Uuid.Empty && reference.Bytes > 0 && Sha256(reference.Sha256) &&
        reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType &&
        reference.Schema == SnapshotSourcePublicationManifest.Schema;

    static bool Sha256(string value) => value is { Length: 64 } && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
