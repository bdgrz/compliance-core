using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

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
