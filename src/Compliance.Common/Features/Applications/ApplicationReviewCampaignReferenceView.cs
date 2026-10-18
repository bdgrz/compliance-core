using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Minimal status metadata for one campaign linked to a reviewed system.</summary>
public sealed record ApplicationReviewCampaignReferenceView(Uuid TenantId,
    Uuid ApplicationId, Uuid SystemInstanceId, Uuid CampaignId, string Status,
    Uuid SnapshotId, DateTimeOffset LaunchedAt, Uuid? FinalSnapshotId,
    DateTimeOffset? CompletedAt);
