using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Projectors;

public sealed partial class AccessReviewCampaignImpactProjector(
    IAccessReviewCampaignImpactProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewCampaign.Area),
            AccessReviewCampaignImpactSchema.Projector),
        IProjectorHandler<AccessReviewCampaignLaunched>,
        IProjectorHandler<AccessReviewCampaignCompleted>
{
    public ValueTask HandleAsync(AccessReviewCampaignLaunched ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewCampaignCompleted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
