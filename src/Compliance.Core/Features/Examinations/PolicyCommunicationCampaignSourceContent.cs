using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record PolicyCommunicationCampaignSourceContent(DateOnly AsOf,
    PolicyCampaignLaunched Launch, int FrozenAudienceBatchCount,
    IReadOnlyList<CampaignAudienceEntry> FrozenAudience,
    IReadOnlyList<PolicyCampaignAudienceAmended> AudienceAmendments,
    IReadOnlyList<PolicyAcknowledgementRecorded> Acknowledgements,
    IReadOnlyList<TrainingCompletionRecorded> Completions,
    IReadOnlyList<CampaignWaiverApproved> Waivers, PolicyCampaignClosed? Closure);
