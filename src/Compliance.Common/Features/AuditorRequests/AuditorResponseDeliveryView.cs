using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>An intentional delivery receipt for an exact submitted response version.</summary>
public sealed record AuditorResponseDeliveryView(
    int Version,
    string Recipient,
    string DeliveryReference,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset DeliveredAt);
