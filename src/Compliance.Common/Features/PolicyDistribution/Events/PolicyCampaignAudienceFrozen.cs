using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The complete frozen launch audience. <paramref name="EntriesManifest" /> holds every
///     <see cref="CampaignAudienceEntry" /> in canonical person order, with no count cap.
/// </summary>
[Discriminator("bdgrz.policy_campaign.audience.frozen", 2)]
public sealed record PolicyCampaignAudienceFrozen(Uuid TenantId, Uuid CampaignId, int Batch,
    ImmutableContentRef EntriesManifest, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
