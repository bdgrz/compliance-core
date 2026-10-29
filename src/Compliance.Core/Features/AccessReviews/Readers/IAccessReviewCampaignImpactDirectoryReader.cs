using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Readers;

public interface IAccessReviewCampaignImpactDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    ValueTask<Page<AccessReviewCampaignSystemInstanceReferenceView>> ListBySystemInstanceAsync(
        Uuid tenantId, Uuid systemInstanceId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<AccessReviewCampaignSummaryView?> GetCampaignAsync(Uuid tenantId,
        Uuid campaignId, CancellationToken ct = default);
}
