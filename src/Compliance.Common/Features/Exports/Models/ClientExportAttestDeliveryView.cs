using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Models;

/// <summary>An attributable delivery decision for an exact attest collaboration package.</summary>
public sealed record ClientExportAttestDeliveryView(
    Uuid EngagementId,
    Uuid PackageId,
    string ManifestSha256,
    string Decision,
    string Rationale,
    string? Recipient,
    string? DeliveryReference,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt);
