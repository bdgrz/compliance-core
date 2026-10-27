using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>Reads aggregate campaign evidence; person-level detail needs program management.</summary>
sealed class GetCampaignHandler(CampaignSource campaigns, TimeProvider clock)
    : IRequestHandler<GetCampaign, CampaignView>
{
    public ValueTask<Result<CampaignView>> HandleAsync(IRequestContext<GetCampaign> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<CampaignView>> HandleCoreAsync(IRequestContext<GetCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await campaigns.ReadAsync(request.TenantId, request.ProgramId, request.CampaignId, ct)
            .ConfigureAwait(false);
        return campaign.IsSuccess
            ? Result<CampaignView>.Success(campaign.Value.ToView(request.AsOf ?? PolicySource.Today(clock)))
            : Result<CampaignView>.Failure(campaign.Error);
    }
}
