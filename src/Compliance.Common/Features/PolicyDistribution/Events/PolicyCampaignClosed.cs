using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The terminal closure of a campaign, appended by the closure process after every admitted participant result
///     was applied and every participant use was held. <paramref name="TotalsContent" /> is the complete
///     <see cref="CampaignTotalsView" />; <paramref name="ParticipantProofs" /> the complete participant proof list.
/// </summary>
[Discriminator("bdgrz.policy_campaign.closed", 2)]
public sealed record PolicyCampaignClosed(Uuid TenantId, Uuid CampaignId, DateOnly ClosedOn,
    string Rationale, ImmutableContentRef TotalsContent, ActorReference Actor, DateTimeOffset ClosedAt,
    OperationProvenance Operation, long AudienceGeneration, ImmutableContentRef ParticipantProofs,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
