using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

static class AccessReviewCampaignImpactSchema
{
    public const string Projector = "ApplicationReviewCampaignImpactV1";

    public static readonly KvDirectoryIndex<AccessReviewCampaignSystemInstanceReferenceView>
        BySystemInstance = new("by_system_instance_newest_first", 1,
            static reference =>
            [
                reference.SystemInstanceId.ToString(),
                (long.MaxValue - reference.LaunchedAt.ToUniversalTime().Ticks)
                    .ToString("D20", CultureInfo.InvariantCulture),
                reference.CampaignId.ToString(),
            ]);

    public static readonly KvDirectory<AccessReviewCampaignSystemInstanceReferenceView, Uuid>
        SystemInstanceReferences = new("access_review_campaign_system_instances",
            ComplianceCoreJsonContext.Default.AccessReviewCampaignSystemInstanceReferenceView,
            static reference => Uuid.CreateVersion5(reference.CampaignId,
                $"system-instance:{reference.SystemInstanceId}"),
            static id => [id.ToString()], [BySystemInstance]);

    public static readonly KvDirectory<AccessReviewCampaignImpactSummaryView, Uuid> Campaigns = new(
        "access_review_campaign_impact_summaries",
        ComplianceCoreJsonContext.Default.AccessReviewCampaignImpactSummaryView,
        static campaign => campaign.Summary.CampaignId,
        static id => [id.ToString()], []);

    public static EventStreamPattern Pattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), AccessReviewCampaign.Area);
}
