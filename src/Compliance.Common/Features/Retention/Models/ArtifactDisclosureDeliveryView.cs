using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>An exact, attributable delivery receipt for an approved disclosure.</summary>
public sealed record ArtifactDisclosureDeliveryView(
    string DeliveryReference,
    ActorReference DeliveredBy,
    DateTimeOffset DeliveredAt,
    ulong SourcePosition);
