using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Projectors;

public sealed partial class AccessReviewCampaignDirectoryProjector(
    IAccessReviewCampaignDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewCampaign.Area),
            AccessReviewDirectorySchema.CampaignProjector),
        IProjectorHandler<AccessReviewCampaignLaunched>,
        IProjectorHandler<AccessReviewCampaignCompleted>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewCampaignLaunched> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewCampaignCompleted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
