using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignReconciliationView(Uuid CampaignId, long Reconciliation,
    Uuid RosterSnapshotId, IReadOnlyList<CampaignAudienceAmendment> Amendments);
