using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class PolicyCampaignWorkItemProjector(
    IPolicyCampaignWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(),
        FitzPolicyCampaignWorkItemDirectory.ProjectorName),
      IProjectorHandler<PolicyCampaignLaunched>, IProjectorHandler<PolicyCampaignAudienceFrozen>,
      IProjectorHandler<PolicyCampaignAudienceAmended>,
      IProjectorHandler<PolicyAcknowledgementRecorded>,
      IProjectorHandler<TrainingCompletionRecorded>, IProjectorHandler<CampaignWaiverApproved>,
      IProjectorHandler<PolicyCampaignClosed>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyCampaignLaunched> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyCampaignAudienceFrozen> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyCampaignAudienceAmended> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyAcknowledgementRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TrainingCompletionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CampaignWaiverApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyCampaignClosed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
