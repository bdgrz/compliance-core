using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Work.Projections;

static class AccessReviewCampaignWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<AccessReviewCampaignWorkItemState> ItemsByProgram =
        new("by_program", 1, static item =>
            [item.ProgramId.ToString(), item.CampaignId.ToString(), item.ItemId.ToString()]);

    public static readonly KvDirectory<AccessReviewCampaignWorkItemState, string> Items = new(
        "items", ComplianceCoreJsonContext.Default.AccessReviewCampaignWorkItemState,
        static item => ItemKey(item.CampaignId, item.ItemId),
        static key => [key], [ItemsByProgram]);

    public static string ItemKey(Uuid campaignId, Uuid itemId) => $"{campaignId}:{itemId}";
}
