using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Source reconciliation, build retries, attest package deliveries, and final receipt.</summary>
public sealed record ClientExportView(
    Uuid TenantId,
    Uuid ClientExportId,
    string Status,
    long Revision,
    ClientExportContent Content,
    string ContentSha256,
    Uuid RequestedByMemberId,
    string RequestedByDisplay,
    DateTimeOffset RequestedAt,
    IReadOnlyList<ClientExportBuildAttemptView> BuildAttempts,
    IReadOnlyList<ClientExportAttestDeliveryView> AttestPackageDeliveries,
    ClientExportDeliveryView? Delivery,
    bool IsComplete,
    Uuid? ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt,
    ActorReference? StoredActor = null)
{
    public ActorReference EffectiveActor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId ?? Uuid.Empty, ActorDisplay);
}
