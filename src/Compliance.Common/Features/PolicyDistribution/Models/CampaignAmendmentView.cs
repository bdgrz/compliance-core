using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignAmendmentView(long Reconciliation, Uuid PersonId,
    string DisplayName, string Reason, DateOnly? DueOn, Uuid RosterSnapshotId,
    string RosterContentSha256, ActorReference Actor, DateTimeOffset AmendedAt);
