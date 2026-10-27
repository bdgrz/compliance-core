using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Attributed delivery of the exact retained archive and manifest to one approved recipient.</summary>
public sealed record AuditPackageDeliveryView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPackageDeliveryId,
    Uuid AuditPackageId,
    int PackageVersion,
    Uuid ManifestId,
    string ManifestSha256,
    Uuid ArchiveArtifactId,
    string ArchiveSha256,
    string Recipient,
    string DeliveryReference,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt,
    Uuid? ActorMemberId,
    string ActorDisplay);
