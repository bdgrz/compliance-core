using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

sealed class ListCampaignAmendmentsHandler(CampaignSource campaigns)
    : IRequestHandler<ListCampaignAmendments, Page<CampaignAmendmentView>>
{
    public ValueTask<Result<Page<CampaignAmendmentView>>> HandleAsync(
        IRequestContext<ListCampaignAmendments> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<Page<CampaignAmendmentView>>> HandleCoreAsync(
        IRequestContext<ListCampaignAmendments> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await campaigns.ReadAsync(request.TenantId, request.ProgramId, request.CampaignId, ct)
            .ConfigureAwait(false);
        return campaign.IsSuccess
            ? ControlActivationSource.Paginate(campaign.Value.Roster.Amendments, request.Limit, request.Cursor,
                "campaign amendments")
            : Result<Page<CampaignAmendmentView>>.Failure(campaign.Error);
    }
}
