using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>An exact, attributable delivery receipt for an approved disclosure.</summary>
public sealed record ArtifactDisclosureDeliveryView(
    string DeliveryReference,
    ActorReference DeliveredBy,
    DateTimeOffset DeliveredAt,
    ulong SourcePosition);
