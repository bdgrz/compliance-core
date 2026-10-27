using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The frozen launch of a campaign read from its launch fact and verified content: the instructions, deadline,
///     reviewers and every item exactly as launched.
/// </summary>
public sealed record AccessReviewLaunch(Uuid CampaignId, string Name, string Instructions, DateTimeOffset Deadline,
    Uuid SnapshotId, string ContentSha256, IReadOnlyList<AccessReviewerView> Reviewers,
    IReadOnlyList<AccessReviewItemView> Items, ActorReference LaunchedBy, DateTimeOffset LaunchedAt,
    Uuid? ProgramId, Uuid? RemediationOwnerMemberId);
