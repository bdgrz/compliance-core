using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Projectors;

public sealed partial class CampaignDirectoryProjector(ICampaignDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policy-distribution-campaigns"),
            FitzCampaignDirectory.ProjectorName),
      IProjectorHandler<PolicyCampaignLaunched>, IProjectorHandler<PolicyCampaignClosed>
{
    public ValueTask HandleAsync(PolicyCampaignLaunched ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyCampaignClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
