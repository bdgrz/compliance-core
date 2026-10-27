using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>A tenant-scoped link between a frozen campaign and one reviewed system.</summary>
public sealed record AccessReviewCampaignSystemInstanceReferenceView(Uuid TenantId,
    Uuid CampaignId, Uuid SystemInstanceId, DateTimeOffset LaunchedAt);
