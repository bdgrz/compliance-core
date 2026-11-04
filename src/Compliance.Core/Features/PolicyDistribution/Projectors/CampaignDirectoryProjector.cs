using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projectors;

public sealed partial class CampaignDirectoryProjector(ICampaignDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policy-distribution-campaigns"),
            FitzCampaignDirectory.ProjectorName),
      IProjectorHandler<PolicyCampaignLaunched>, IProjectorHandler<PolicyCampaignClosed>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PolicyCampaignLaunched> context,
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
