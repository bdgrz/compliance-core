using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>
///     A campaign evaluated as of a date. <c>Status</c> is <c>open</c>, <c>closing</c> (result admission is sealed
///     and closure is pending) or <c>closed</c>. <c>Revision</c> is the campaign's logical revision.
/// </summary>
public sealed record CampaignView(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    CampaignSubject Subject, string AudienceKind, IReadOnlyList<string> AudienceTeams,
    Uuid RosterSnapshotId, string RosterContentSha256, Uuid LatestRosterSnapshotId,
    DateOnly LaunchedOn, DateOnly DueOn, string? Instructions, string? AcknowledgementText,
    string Status, DateOnly AsOf, CampaignTotalsView Totals, ActorReference LaunchedBy,
    DateTimeOffset LaunchedAt, ActorReference? ClosedBy, DateTimeOffset? ClosedAt,
    CampaignTotalsView? ClosingTotals, long Revision);
