using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

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
