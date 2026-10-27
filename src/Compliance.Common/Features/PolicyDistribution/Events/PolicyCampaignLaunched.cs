using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Launches a campaign bound to exact material and a frozen roster snapshot. The audience
///     itself follows as one <see cref="PolicyCampaignAudienceFrozen" /> fact in the same batch.
///     <paramref name="AudienceTeamsManifest" /> is the complete original list of team names.
/// </summary>
[Discriminator("bdgrz.policy_campaign.launched", 2)]
public sealed record PolicyCampaignLaunched(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    CampaignSubject Subject, string AudienceKind, ImmutableContentRef AudienceTeamsManifest,
    Uuid RosterSnapshotId, string RosterContentSha256, DateOnly LaunchedOn, DateOnly DueOn,
    string? Instructions, string? AcknowledgementText, int AudienceCount,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset LaunchedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
