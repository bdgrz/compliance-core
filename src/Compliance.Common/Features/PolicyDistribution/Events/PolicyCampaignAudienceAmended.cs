using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     A reconciliation against a later roster snapshot. The frozen launch audience is never edited; a
///     reconciliation with no changes still records its snapshot. <paramref name="AmendmentsManifest" /> holds
///     the complete list of <see cref="CampaignAudienceAmendment" /> in order.
/// </summary>
[Discriminator("bdgrz.policy_campaign.audience.amended", 2)]
public sealed record PolicyCampaignAudienceAmended(Uuid TenantId, Uuid CampaignId,
    long Reconciliation, int Batch, Uuid RosterSnapshotId, string RosterContentSha256,
    DateOnly ObservedOn, ImmutableContentRef AmendmentsManifest,
    ActorReference Actor, DateTimeOffset AmendedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
