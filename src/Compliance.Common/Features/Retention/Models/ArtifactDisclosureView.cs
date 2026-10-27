using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>Approval, delivery, and revocation history for one external disclosure.</summary>
public sealed record ArtifactDisclosureView(
    Uuid TenantId,
    Uuid DisclosureId,
    Uuid ArtifactId,
    ArtifactRetentionSource Source,
    long ContentLength,
    string HandlingClass,
    long HandlingPolicyRevision,
    ulong ApprovedSourcePosition,
    string Recipient,
    string Purpose,
    string Rationale,
    ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt,
    string Status,
    long Revision,
    ArtifactDisclosureDeliveryView? Delivery,
    ActorReference? RevokedBy,
    string? RevocationRationale,
    DateTimeOffset? RevokedAt,
    bool DownloadedCopiesRecallable);
