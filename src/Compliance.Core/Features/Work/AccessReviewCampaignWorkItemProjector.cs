using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Projects the accountable work of each access review item from the item's own stream. A closed campaign
///     settles every item use with the closure fence, which is the item fact that completes its work.
/// </summary>
public sealed partial class AccessReviewCampaignWorkItemProjector(
    IAccessReviewCampaignWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewItem.Area),
        FitzAccessReviewCampaignWorkItemDirectory.ProjectorName),
      IProjectorHandler<AccessReviewItemOpened>,
      IProjectorHandler<AccessDecisionsRecorded>,
      IProjectorHandler<AccessRemediationChangeRecorded>,
      IProjectorHandler<AccessRemediationVerified>,
      IProjectorHandler<AccessRemediationExceptionRecorded>,
      IProjectorHandler<AccessReviewResponsibilityReassigned>,
      IProjectorHandler<AccessReviewItemCampaignCompletionUseSettled>
{
    public ValueTask HandleAsync(AccessReviewItemOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessDecisionsRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationChangeRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationVerified ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationExceptionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewResponsibilityReassigned ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewItemCampaignCompletionUseSettled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
