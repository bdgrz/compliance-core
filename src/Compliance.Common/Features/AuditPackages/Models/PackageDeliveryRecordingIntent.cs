using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>One personal handoff, pinned to its exact archive, disclosure and complete validation proofs.</summary>
public sealed record PackageDeliveryRecordingIntent(DurableIntentHeader Header, Uuid DeliveryId, Uuid PackageId,
    long PackageVersion, Uuid ManifestId, string ManifestSha256, Uuid ArchiveArtifactId, string ArchiveSha256,
    string Recipient, string DeliveryReference, DateTimeOffset DeliveredAt, DateTimeOffset RecordedAt,
    Uuid RecorderMemberId, NativeFactReference DisclosureApproval, ImmutableSelectionManifest ValidationProofs)
{
    public const string Schema = "package-delivery-recording-intent.v1";
}
