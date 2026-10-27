using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

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
      IProjectorHandler<AccessReviewItemCampaignCompletionUseSettled>,
      IProjectorHandler<AccessReviewItemDecisionSettled>
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

    public ValueTask HandleAsync(AccessReviewItemDecisionSettled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
