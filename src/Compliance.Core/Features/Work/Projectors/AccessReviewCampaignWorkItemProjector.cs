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
    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewItemOpened> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessDecisionsRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessRemediationChangeRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessRemediationVerified> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessRemediationExceptionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewResponsibilityReassigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewItemCampaignCompletionUseSettled> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<AccessReviewItemDecisionSettled> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
