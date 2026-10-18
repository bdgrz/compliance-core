using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>The recipient and receipt retained for final export delivery.</summary>
public sealed record ClientExportDeliveryView(
    string Recipient,
    string DeliveryReference,
    Uuid ArchiveArtifactId,
    string ArchiveSha256,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset DeliveredAt);
