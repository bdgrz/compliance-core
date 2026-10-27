using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Queries;

/// <summary>Previews a bulk decision for the acting reviewer without recording it.</summary>
[Discriminator("bdgrz.access_review.decision.bulk_preview", 1)]
public sealed record PreviewBulkAccessDecision(Uuid TenantId, Uuid CampaignId,
    IReadOnlyList<Uuid> ItemIds, string Decision)
    : IRequest<BulkAccessDecisionPreview>, IAccessReviewParticipantRequest, ICallable;
