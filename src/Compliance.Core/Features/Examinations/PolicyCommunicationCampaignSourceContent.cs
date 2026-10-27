using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
///     The complete point-in-time content of a campaign for a Type I baseline. The teams, the frozen audience, every
///     amendment's items and the closing totals are the expanded originals of the manifests the campaign facts
///     reference, so the digest commits to all of them.
/// </summary>
sealed record PolicyCommunicationCampaignSourceContent(DateOnly AsOf,
    PolicyCampaignLaunched Launch, IReadOnlyList<string> AudienceTeams, int FrozenAudienceBatchCount,
    IReadOnlyList<CampaignAudienceEntry> FrozenAudience,
    IReadOnlyList<PolicyCommunicationAudienceAmendment> AudienceAmendments,
    IReadOnlyList<PolicyAcknowledgementRecorded> Acknowledgements,
    IReadOnlyList<TrainingCompletionRecorded> Completions,
    IReadOnlyList<CampaignWaiverApproved> Waivers, PolicyCampaignClosed? Closure,
    CampaignTotalsView? ClosingTotals);

/// <summary>One reconciliation fact with the complete list of amendments its manifest holds.</summary>
sealed record PolicyCommunicationAudienceAmendment(PolicyCampaignAudienceAmended Fact,
    IReadOnlyList<CampaignAudienceAmendment> Items);
